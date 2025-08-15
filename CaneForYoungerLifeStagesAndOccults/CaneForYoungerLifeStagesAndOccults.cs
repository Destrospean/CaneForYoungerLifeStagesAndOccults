using Sims3.Gameplay;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.EventSystem;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects;
using Sims3.Gameplay.Pools;
using Sims3.Gameplay.ThoughtBalloons;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI.Hud;
using System;
using System.Collections.Generic;
using Tuning = Sims3.Gameplay.Destrospean.CaneForYoungerLifeStagesAndOccults;

namespace Destrospean
{
    public class CaneForYoungerLifeStagesAndOccults
    {
        [Tunable]
        protected static bool kInstantiator;

        static CaneForYoungerLifeStagesAndOccults()
        {
            kInstantiator = false;
            LoadSaveManager.ObjectGroupsPreLoad += OnPreLoad;
        }

        public class HarassEveryone : Cane.HarassEveryone
        {
            public class DefinitionModified : InteractionDefinition<Sim, Terrain, HarassEveryone>
            {
                public override bool Test(Sim actor, Terrain target, bool isAutonomous, ref GreyedOutTooltipCallback greyedOutTooltipCallback)
                {
                    if (actor.Posture is SwimmingInPool || !IsAllowedToUseCane(actor))
                    {
                        return false;
                    }
                    return actor.Inventory.FindAll<Cane>(false).Count > 0;
                }

                public override InteractionTestResult Test(ref InteractionInstanceParameters parameters, ref GreyedOutTooltipCallback greyedOutTooltipCallback)
                {
                    if (parameters.Hit.mType != GameObjectHitType.Terrain && parameters.Hit.mType != GameObjectHitType.LotTerrain && parameters.Hit.mType != GameObjectHitType.Object)
                    {
                        return InteractionTestResult.Gen_BadTerrainType;
                    }
                    InteractionTestResult interactionTestResult = base.Test(ref parameters, ref greyedOutTooltipCallback);
                    if (interactionTestResult != 0)
                    {
                        return interactionTestResult;
                    }
                    if (Terrain.GoHere.SharedGoHereTests(ref parameters))
                    {
                        Vector3 point = parameters.Hit.mPoint;
                        Route route = parameters.Actor.CreateRoute();
                        if (route.IsPointRoutable(parameters.Hit.mPoint) && !World.IsInPool(point))
                        {
                            return InteractionTestResult.Pass;
                        }
                        return InteractionTestResult.GenericFail;
                    }
                    return InteractionTestResult.GenericFail;
                }
            }

            public override bool Run()
            {
                GameObject gameObject = null;
                if (RandomUtil.RandomChance01(kChanceToHarassObject))
                {
                    LotLocation location = LotLocation.Invalid;
                    ulong lotLocation = World.GetLotLocation(Destination, ref location);
                    int roomId = World.GetRoomId(lotLocation, location);
                    GameObject[] objects = Sims3.Gameplay.Queries.GetObjects<GameObject>(Destination, kHarassEveryoneObjectSearchRadius);
                    List<GameObject> list = new List<GameObject>();
                    foreach (GameObject obj in objects)
                    {
                        if (obj.RoomId == roomId && !obj.InUse && obj != Actor)
                        {
                            list.Add(obj);
                        }
                    }
                    if (list.Count > 0)
                    {
                        gameObject = RandomUtil.GetRandomObjectFromList(list);
                    }
                }
                Route route = Actor.CreateRoute();
                route.PlanToPointRadialRange(Destination, kHarassEveryoneObjectRouteRadiusRange[0], kHarassEveryoneObjectRouteRadiusRange[1], RouteDistancePreference.PreferNearestToRouteDestination, RouteOrientationPreference.NoPreference, 0uL, null);
                if (!Actor.DoRoute(route))
                {
                    return false;
                }
                if (gameObject != null)
                {
                    Actor.RouteTurnToFace(gameObject.Position);
                }
                else
                {
                    Actor.RouteTurnToFace(Destination + RandomUtil.GetRandomDirXZ());
                }
                if (Actor.Posture is SwimmingInPool)
                {
                    Actor.PlayRouteFailure();
                    return false;
                }
                mCane = Actor.GetActiveCane() ?? Actor.Inventory.Find<Cane>();
                if (mCane == null)
                {
                    return false;
                }
                mCane.CreatePropCane();
                StandardEntry();
                BeginCommodityUpdates();
                EnterStateMachine("ElderCane", "Enter", "x");
                SetActor("Cane", mCane.PropCane);
                mCurrentStateMachine.AddOneShotStateEnteredEventHandler("Harass Everyone", FadePropCaneIn);
                if (!(Actor.Posture is Cane.HoldingCanePosture))
                {
                    mCurrentStateMachine.AddOneShotStateEnteredEventHandler("Exit", FadePropCaneOut);
                }
                if (gameObject != null)
                {
                    ThoughtBalloonManager.BalloonData balloonData = new ThoughtBalloonManager.BalloonData(gameObject.GetThoughtBalloonThumbnailKey());
                    balloonData.BalloonType = ThoughtBalloonTypes.kThoughtBalloon;
                    balloonData.Duration = ThoughtBalloonDuration.Short;
                    balloonData.LowAxis = ThoughtBalloonAxis.kDislike;
                    Actor.ThoughtBalloonManager.ShowBalloon(balloonData);
                }
                AnimateSim("Harass Everyone");
                EventTracker.SendEvent(EventTypeId.kHarassWithCane, Actor);
                AnimateSim("Exit");
                EndCommodityUpdates(true);
                StandardExit();
                return true;
            }
        }

        public class SetWalkStyle : Cane.SetWalkStyle
        {
            [DoesntRequireTuning]
            public class DefinitionModified : ImmediateInteractionDefinition<Sim, Cane, SetWalkStyle>
            {
                public Sim.WalkStyle CaneWalkStyle;

                public DefinitionModified()
                {
                }

                public DefinitionModified(Sim.WalkStyle walkStyle)
                {
                    CaneWalkStyle = walkStyle;
                }

                public override void AddInteractions(InteractionObjectPair interaction, Sim actor, Cane target, List<InteractionObjectPair> results)
                {
                    DefinitionModified definition = new DefinitionModified(Cane.kSouthernGentlemanCaneWalk);
                    results.Add(new InteractionObjectPair(definition, target));
                    definition = new DefinitionModified(Cane.kElderlyCaneWalk);
                    results.Add(new InteractionObjectPair(definition, target));
                }

                public override string GetInteractionName(Sim actor, Cane target, InteractionObjectPair iop)
                {
                    if (CaneWalkStyle == Cane.kSouthernGentlemanCaneWalk)
                    {
                        return Cane.LocalizeString(actor.IsFemale, "SouthernGentleman");
                    }
                    return Cane.LocalizeString(actor.IsFemale, "Elderly");
                }

                public override bool Test(Sim actor, Cane target, bool isAutonomous, ref GreyedOutTooltipCallback greyedOutTooltipCallback)
                {
                    if (!IsAllowedToUseCane(actor))
                    {
                        return false;
                    }
                    if (actor.SimDescription.TeenOrBelow)
                    {
                        greyedOutTooltipCallback = CreateTooltipCallback(Cane.LocalizeString(actor.IsFemale, "AgeRestriction", actor));
                        return false;
                    }
                    if (CaneWalkStyle == target.CurrentCaneWalkStyle)
                    {
                        greyedOutTooltipCallback = CreateTooltipCallback(Cane.LocalizeString(actor.IsFemale, "WalkStyleInUse", actor));
                        return false;
                    }
                    return true;
                }
            }

            public override bool RunFromInventory()
            {
                DefinitionModified definition = InteractionDefinition as DefinitionModified;
                Target.CurrentCaneWalkStyle = definition.CaneWalkStyle;
                return true;
            }
        }

        public class TraitHarassWithCane : Cane.TraitHarassWithCane
        {
            public class DefinitionModified : SoloSimInteractionDefinition<TraitHarassWithCane>
            {
                public override bool Test(Sim actor, Sim target, bool isAutonomous, ref GreyedOutTooltipCallback greyedOutTooltipCallback)
                {
                    if (!IsAllowedToUseCane(actor))
                    {
                        return false;
                    }
                    if (actor.Inventory.ContainsType(typeof(Cane), 1))
                    {
                        return actor.TraitManager.HasAnyElement(Cane.TraitHarassWithCane.HarassTraits);
                    }
                    return false;
                }
            }

            public override bool Run()
            {
                if (mHarassmentObject == null)
                {
                    return false;
                }
                World.FindGoodLocationParams locationParams = new World.FindGoodLocationParams(mHarassmentObject.Position);
                locationParams.StartPosition = mHarassmentObject.Position;
                locationParams.RequiredRoomID = mHarassmentObject.RoomId;
                bool routeDone = false;
                if (GlobalFunctions.FindGoodLocation(Actor, locationParams, out var pos, out var _))
                {
                    Route route = Actor.CreateRoute();
                    route.PlanToPoint(pos);
                    route.DoRouteFail = false;
                    routeDone = Actor.DoRoute(route);
                }
                if (!routeDone)
                {
                    Actor.RouteToDynamicObjectRadiusWithCondition(mHarassmentObject, kObjectFallbackRouteRadius[0], kObjectFallbackRouteRadius[1], null, null, Route.RouteOption.DoLineOfSightCheckUserOverride);
                }
                Actor.RouteTurnToFace(mHarassmentObject.Position);
                if (Actor.Posture is SwimmingInPool)
                {
                    Actor.PlayRouteFailure();
                    return false;
                }
                mCane = Actor.GetActiveCane() ?? Actor.Inventory.Find<Cane>();
                if (mCane == null)
                {
                    return false;
                }
                if (!mCane.UsingCane)
                {
                    mCane.AddToUseList(Actor);
                }
                mCane.CreatePropCane();
                StandardEntry();
                BeginCommodityUpdates();
                EnterStateMachine("ElderCane", "Enter", "x");
                SetActor("Cane", mCane.PropCane);
                mCurrentStateMachine.AddOneShotStateEnteredEventHandler("Harass Everyone", FadePropCaneIn);
                if (!(Actor.Posture is Cane.HoldingCanePosture))
                {
                    mCurrentStateMachine.AddOneShotStateEnteredEventHandler("Exit", FadePropCaneOut);
                }
                ThoughtBalloonManager.BalloonData balloonData = ThoughtBalloonManager.GetBalloonData(mIcon, Actor);
                balloonData.BalloonType = ThoughtBalloonTypes.kThoughtBalloon;
                balloonData.LowAxis = ThoughtBalloonAxis.kDislike;
                balloonData.Duration = ThoughtBalloonDuration.Short;
                Actor.ThoughtBalloonManager.ShowBalloon(balloonData);
                AnimateSim("Harass Everyone");
                AnimateSim("Exit");
                EndCommodityUpdates(true);
                StandardExit();
                return true;
            }
        }

        public class UseCane : Cane.UseCane
        {
            [DoesntRequireTuning]
            public class DefinitionModified : ImmediateInteractionDefinition<Sim, Cane, UseCane>
            {
                public override string GetInteractionName(Sim actor, Cane target, InteractionObjectPair interaction)
                {
                    if (target.UsingCane)
                    {
                        return Cane.LocalizeString(actor.IsFemale, "StopUsingCane");
                    }
                    return Cane.LocalizeString(actor.IsFemale, "StartUsingCane");
                }

                public override bool Test(Sim actor, Cane target, bool isAutonomous, ref GreyedOutTooltipCallback greyedOutTooltipCallback)
                {
                    if (!IsAllowedToUseCane(actor))
                    {
                        return false;
                    }
                    if (actor.SimDescription.TeenOrBelow)
                    {
                        greyedOutTooltipCallback = CreateTooltipCallback(Cane.LocalizeString(actor.IsFemale, "AgeRestriction", actor));
                        return false;
                    }
                    return true;
                }
            }

            public override bool RunFromInventory()
            {
                if (Target.UsingCane)
                {
                    if (Actor.Posture is Cane.HoldingCanePosture)
                    {
                        if (!Actor.InteractionQueue.HasInteractionOfTypeAndTarget(Cane.StopUsingCane.Singleton, Target))
                        {
                            InteractionInstance stopUsingCane = Cane.StopUsingCane.Singleton.CreateInstance(Target, Actor, new InteractionPriority(InteractionPriorityLevel.UserDirected), false, true);
                            stopUsingCane.Hidden = true;
                            Actor.InteractionQueue.PushAsContinuation(stopUsingCane, true);
                        }
                    }
                    else
                    {
                        Target.UsingCane = false;
                    }
                }
                else if (Actor.Posture is Cane.HoldingCanePosture || Actor.Posture == Actor.Standing)
                {
                    if (!Actor.InteractionQueue.HasInteractionOfTypeAndTarget(Cane.StartUsingCane.Singleton, Target))
                    {
                        InteractionInstance startUsingCane = Cane.StartUsingCane.Singleton.CreateInstance(Target, Actor, new InteractionPriority(InteractionPriorityLevel.UserDirected), false, true);
                        startUsingCane.Hidden = true;
                        Actor.InteractionQueue.PushAsContinuation(startUsingCane, true);
                    }
                }
                else
                {
                    Target.UsingCane = true;
                }
                return true;
            }
        }

        public static void CopyTuning(Type baseType, Type oldType, Type newType)
        {
            if (AutonomyTuning.GetTuning(newType.FullName, baseType.FullName) == null)
            {
                InteractionTuning tuning = AutonomyTuning.GetTuning(oldType, oldType.FullName, baseType);
                if (tuning != null)
                {
                    AutonomyTuning.AddTuning(newType.FullName, baseType.FullName, tuning);
                }
            }
            InteractionObjectPair.sTuningCache.Remove(new Pair<Type, Type>(newType, baseType));
        }

        public static bool IsAllowedToUseCane(Sim sim)
        {
            return !(sim.SimDescription.IsFairy && !Tuning.kUsableForFairies || sim.SimDescription.IsFrankenstein && !Tuning.kUsableForSimBots || sim.SimDescription.IsGenie && !Tuning.kUsableForGenies || sim.SimDescription.IsGhost && !Tuning.kUsableForGhosts || sim.SimDescription.IsImaginaryFriend && !Tuning.kUsableForImaginaryFriends || sim.SimDescription.IsMummy && !Tuning.kUsableForMummies || sim.SimDescription.IsVampire && !Tuning.kUsableForVampires || sim.BuffManager.HasElement(BuffNames.Werewolf) && !Tuning.kUsableInWerewolfForm || sim.BuffManager.HasAnyElement(BuffNames.Zombie, BuffNames.PermaZombie) && !Tuning.kUsableForZombies || sim.IsEP11Bot && !Tuning.kUsableForPlumbots || (sim.SimDescription.IsGhost || sim.SimDescription.DeathStyle != 0) && !Tuning.kUsableForGhosts || sim.IsPet);
        }

        static void OnPreLoad()
        {
            Cane.HarassEveryone.Singleton = new HarassEveryone.DefinitionModified();
            Cane.SetWalkStyle.Singleton = new SetWalkStyle.DefinitionModified();
            Cane.TraitHarassWithCane.Singleton = new TraitHarassWithCane.DefinitionModified();
            Cane.UseCane.Singleton = new UseCane.DefinitionModified();
            CopyTuning(typeof(Cane), typeof(Cane.HarassEveryone.Definition), typeof(HarassEveryone.DefinitionModified));
            CopyTuning(typeof(Cane), typeof(Cane.TraitHarassWithCane.Definition), typeof(TraitHarassWithCane.DefinitionModified));
        }
    }
}
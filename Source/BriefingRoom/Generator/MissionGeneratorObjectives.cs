﻿/*
==========================================================================
This file is part of Briefing Room for DCS World, a mission
generator for DCS World, by @akaAgar (https://github.com/akaAgar/briefing-room-for-dcs)

Briefing Room for DCS World is free software: you can redistribute it
and/or modify it under the terms of the GNU General Public License
as published by the Free Software Foundation, either version 3 of
the License, or (at your option) any later version.

Briefing Room for DCS World is distributed in the hope that it will
be useful, but WITHOUT ANY WARRANTY; without even the implied warranty
of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Briefing Room for DCS World. If not, see https://www.gnu.org/licenses/
==========================================================================
*/

using BriefingRoom4DCS.Data;
using BriefingRoom4DCS.Mission;
using BriefingRoom4DCS.Template;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BriefingRoom4DCS.Generator
{
    /// <summary>
    /// Generates the <see cref="DCSMissionObjective"/> array.
    /// </summary>
    internal class MissionGeneratorObjectives : IDisposable
    {
        /// <summary>
        /// List of available objective names, to make sure each one is different.
        /// </summary>
        private readonly List<string> ObjectiveNames = new List<string>();

        /// <summary>
        /// Minimum objective distance variation.
        /// </summary>
        private const double OBJECTIVE_DISTANCE_VARIATION_MIN = 0.75;

        /// <summary>
        /// Maximum objective distance variation.
        /// </summary>
        private const double OBJECTIVE_DISTANCE_VARIATION_MAX = 1.25;

        /// <summary>
        /// Unit maker selector to use for objective generation.
        /// </summary>
        private readonly UnitMaker UnitMaker;

        /// <summary>
        /// MissionGeneratorFeaturesObjectives to use to generate objective features;
        /// </summary>
        private readonly MissionGeneratorFeaturesObjectives FeaturesGenerator;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="spawnPointSelector">Spawn point selector to use for objective generation</param>
        internal MissionGeneratorObjectives(UnitMaker unitMaker)
        {
            UnitMaker = unitMaker;
            FeaturesGenerator = new MissionGeneratorFeaturesObjectives(unitMaker);
            ObjectiveNames = new List<string>(Database.Instance.Common.Names.WPObjectivesNames);
        }

        internal Coordinates GenerateObjective(DCSMission mission, MissionTemplate template, DBEntryTheater theaterDB, int objectiveIndex, Coordinates lastCoordinates, DBEntryAirbase playerAirbase, out string objectiveName, out UnitFamily objectiveTargetUnitFamily)
        {
            MissionTemplateObjective objectiveTemplate = template.Objectives[objectiveIndex];
            DBEntryFeatureObjective[] featuresDB = Database.Instance.GetEntries<DBEntryFeatureObjective>(objectiveTemplate.Features.ToArray());
            DBEntryObjectiveTarget targetDB = Database.Instance.GetEntry<DBEntryObjectiveTarget>(objectiveTemplate.Target);
            DBEntryObjectiveTargetBehavior targetBehaviorDB = Database.Instance.GetEntry<DBEntryObjectiveTargetBehavior>(objectiveTemplate.TargetBehavior);
            DBEntryObjectiveTask taskDB = Database.Instance.GetEntry<DBEntryObjectiveTask>(objectiveTemplate.Task);

            if (targetDB == null) throw new BriefingRoomException($"Target \"{targetDB.UIDisplayName}\" not found for objective #{objectiveIndex + 1}.");
            if (targetBehaviorDB == null) throw new BriefingRoomException($"Target behavior \"{targetBehaviorDB.UIDisplayName}\" not found for objective #{objectiveIndex + 1}.");
            if (taskDB == null) throw new BriefingRoomException($"Task \"{taskDB.UIDisplayName}\" not found for objective #{objectiveIndex + 1}.");

            if (!taskDB.ValidUnitCategories.Contains(targetDB.UnitCategory))
                throw new BriefingRoomException($"Task \"{taskDB.UIDisplayName}\" not valid for objective #{objectiveIndex + 1} targets, which belong to category \"{targetDB.UnitCategory}\".");

            DBEntryTheaterSpawnPoint? spawnPoint = UnitMaker.SpawnPointSelector.GetRandomSpawnPoint(
                targetDB.ValidSpawnPoints, lastCoordinates,
                new MinMaxD(
                    template.FlightPlanObjectiveDistance * OBJECTIVE_DISTANCE_VARIATION_MIN,
                    template.FlightPlanObjectiveDistance * OBJECTIVE_DISTANCE_VARIATION_MAX),
                null, null, GeneratorTools.GetSpawnPointCoalition(template, Side.Enemy));

            if (!spawnPoint.HasValue) throw new BriefingRoomException($"Failed to spawn objective unit group. {String.Join(",", targetDB.ValidSpawnPoints.Select(x => x.ToString()).ToList())}");

            Coordinates objectiveCoordinates = spawnPoint.Value.Coordinates;

            // Spawn target on airbase
            int airbaseID = 0;
            var parkingSpotIDsList = new List<int>();
            var parkingSpotCoordinatesList = new List<Coordinates>();
            var unitCount = targetDB.UnitCount[(int)objectiveTemplate.TargetCount].GetValue();
            switch (targetBehaviorDB.Location)
            {
                case DBEntryObjectiveTargetBehaviorLocation.SpawnOnAirbase:
                case DBEntryObjectiveTargetBehaviorLocation.SpawnOnAirbaseParking:
                case DBEntryObjectiveTargetBehaviorLocation.SpawnOnAirbaseParkingNoHardenedShelter:
                    DBEntryAirbase targetAirbase =
                        (from DBEntryAirbase airbaseDB in theaterDB.GetAirbases()
                         where airbaseDB.DCSID != playerAirbase.DCSID
                         select airbaseDB).OrderBy(x => x.Coordinates.GetDistanceFrom(objectiveCoordinates)).FirstOrDefault();
                    objectiveCoordinates = targetAirbase.Coordinates;
                    airbaseID = targetAirbase.DCSID;

                    Coordinates? lastParkingCoordinates = null;

                    for (int i = 0; i < unitCount; i++)
                    {
                        int parkingSpot = UnitMaker.SpawnPointSelector.GetFreeParkingSpot(
                            targetAirbase.DCSID,
                            out Coordinates parkingSpotCoordinates,
                            lastParkingCoordinates,
                            targetBehaviorDB.Location == DBEntryObjectiveTargetBehaviorLocation.SpawnOnAirbaseParkingNoHardenedShelter);
                        if (parkingSpot < 0) throw new BriefingRoomException("No parking spot found for aircraft.");
                        lastParkingCoordinates = parkingSpotCoordinates;

                        parkingSpotIDsList.Add(parkingSpot);
                        parkingSpotCoordinatesList.Add(parkingSpotCoordinates);
                    }
                    break;
            }

            // Pick a name, then remove it from the list
            objectiveName = Toolbox.RandomFrom(ObjectiveNames);
            ObjectiveNames.Remove(objectiveName);

            UnitMakerGroupFlags groupFlags = 0;
            if (objectiveTemplate.Options.Contains(ObjectiveOption.ShowTarget)) groupFlags = UnitMakerGroupFlags.NeverHidden;
            else if (objectiveTemplate.Options.Contains(ObjectiveOption.HideTarget)) groupFlags = UnitMakerGroupFlags.AlwaysHidden;

            objectiveTargetUnitFamily = Toolbox.RandomFrom(targetDB.UnitFamilies);

            // Set destination point for moving unit groups
            Coordinates destinationPoint = objectiveCoordinates;
            switch (targetDB.UnitCategory)
            {
                default:
                    destinationPoint += Coordinates.CreateRandom(10, 20) * Toolbox.NM_TO_METERS;
                    break;
                case UnitCategory.Plane:
                    destinationPoint += Coordinates.CreateRandom(30, 60) * Toolbox.NM_TO_METERS;
                    break;
            }

            switch (targetBehaviorDB.Location)
            {
                case DBEntryObjectiveTargetBehaviorLocation.GoToPlayerAirbase:
                    destinationPoint = playerAirbase.Coordinates;
                    break;
            }

            var extraSettings = new List<KeyValuePair<string, object>>{
                "GroupX2".ToKeyValuePair(destinationPoint.X),
                "GroupY2".ToKeyValuePair(destinationPoint.Y),
                "GroupAirbaseID".ToKeyValuePair(airbaseID),
                "ParkingID".ToKeyValuePair(parkingSpotIDsList.ToArray())
            };
            if (parkingSpotCoordinatesList.Count > 1)
            {
                extraSettings.Add("UnitX".ToKeyValuePair((from Coordinates coordinates in parkingSpotCoordinatesList select coordinates.X).ToArray()));
                extraSettings.Add("UnitY".ToKeyValuePair((from Coordinates coordinates in parkingSpotCoordinatesList select coordinates.Y).ToArray()));
            }
            UnitMakerGroupInfo? targetGroupInfo = UnitMaker.AddUnitGroup(
                objectiveTargetUnitFamily, unitCount,
                taskDB.TargetSide,
                targetBehaviorDB.GroupLua[(int)targetDB.UnitCategory], targetBehaviorDB.UnitLua[(int)targetDB.UnitCategory],
                objectiveCoordinates,
                null, groupFlags,
                AircraftPayload.Default,
                extraSettings.ToArray());

            if (!targetGroupInfo.HasValue) // Failed to generate target group
                throw new BriefingRoomException($"Failed to generate group for objective {objectiveIndex + 1}");

            // Get tasking string for the briefing
            int pluralIndex = targetGroupInfo.Value.UnitsID.Length == 1 ? 0 : 1; // 0 for singular, 1 for plural. Used for task/names arrays.
            string taskString = GeneratorTools.ParseRandomString(taskDB.BriefingTask[pluralIndex]).Replace("\"", "''");
            if (string.IsNullOrEmpty(taskString)) taskString = "Perform task at objective $OBJECTIVENAME$";
            GeneratorTools.ReplaceKey(ref taskString, "ObjectiveName", objectiveName);
            GeneratorTools.ReplaceKey(ref taskString, "UnitFamily", Database.Instance.Common.Names.UnitFamilies[(int)objectiveTargetUnitFamily][pluralIndex]);
            mission.Briefing.AddItem(DCSMissionBriefingItemType.Task, taskString);

            // Add Lua table for this objective
            string objectiveLua = $"briefingRoom.mission.objectives[{objectiveIndex + 1}] = {{ ";
            objectiveLua += $"complete = false, ";
            objectiveLua += $"groupID = {targetGroupInfo.Value.GroupID}, ";
            objectiveLua += $"name = \"{objectiveName}\", ";
            objectiveLua += $"targetCategory = Unit.Category.{targetDB.UnitCategory.ToLuaName()}, ";
            objectiveLua += $"task = \"{taskString}\", ";
            objectiveLua += $"unitsCount = {targetGroupInfo.Value.UnitsID.Length}, ";
            objectiveLua += $"unitsID = {{ {string.Join(", ", targetGroupInfo.Value.UnitsID)} }} ";
            objectiveLua += "}\n";

            // Add F10 sub-menu for this objective
            objectiveLua += $"briefingRoom.f10Menu.objectives[{objectiveIndex + 1}] = missionCommands.addSubMenuForCoalition(coalition.side.{template.ContextPlayerCoalition.ToString().ToUpperInvariant()}, \"Objective {objectiveName}\", nil)\n";
            mission.AppendValue("ScriptObjectives", objectiveLua);

            // Add objective trigger Lua for this objective
            string triggerLua = Toolbox.ReadAllTextIfFileExists($"{BRPaths.INCLUDE_LUA_OBJECTIVETRIGGERS}{taskDB.CompletionTriggerLua}");
            GeneratorTools.ReplaceKey(ref triggerLua, "ObjectiveIndex", objectiveIndex + 1);
            mission.AppendValue("ScriptObjectivesTriggers", triggerLua);

            // Add objective features Lua for this objective
            mission.AppendValue("ScriptObjectivesFeatures", ""); // Just in case there's no features
            foreach (string featureID in objectiveTemplate.Features.ToArray())
                FeaturesGenerator.GenerateMissionFeature(mission, featureID, objectiveName, objectiveIndex, targetGroupInfo.Value.GroupID, objectiveCoordinates);

            return objectiveCoordinates;
        }

        internal Waypoint GenerateObjectiveWaypoint(MissionTemplateObjective objectiveTemplate, Coordinates objectiveCoordinates, string objectiveName)
        {
            DBEntryObjectiveTarget targetDB = Database.Instance.GetEntry<DBEntryObjectiveTarget>(objectiveTemplate.Target);
            if (targetDB == null) throw new BriefingRoomException($"Target \"{targetDB.UIDisplayName}\" not found for objective.");

            Coordinates waypointCoordinates = objectiveCoordinates;
            bool onGround = !targetDB.UnitCategory.IsAircraft(); // Ground targets = waypoint on the ground

            if (objectiveTemplate.Options.Contains(ObjectiveOption.InaccurateWaypoint))
                waypointCoordinates += Coordinates.CreateRandom(3.0, 6.0) * Toolbox.NM_TO_METERS;

            return new Waypoint(objectiveName, waypointCoordinates, onGround);
        }

        /// <summary>
        /// <see cref="IDisposable"/> implementation.
        /// </summary>
        public void Dispose()
        {
            FeaturesGenerator.Dispose();
        }
    }
}

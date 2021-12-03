/*
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

using BriefingRoom4DCS.Campaign;
using BriefingRoom4DCS.Data;
using BriefingRoom4DCS.Generator;
using BriefingRoom4DCS.Mission;
using BriefingRoom4DCS.Template;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BriefingRoom4DCS
{
    public sealed class BriefingRoom : IDisposable
    {
        public static string TARGETED_DCS_WORLD_VERSION { get; private set; }

        public const string REPO_URL = "https://github.com/akaAgar/briefing-room-for-dcs";

        public const string WEBSITE_URL = "https://akaagar.itch.io/briefing-room-for-dcs";

        public const string VERSION = "0.5.111.18";

        public delegate void LogHandler(string message, LogMessageErrorLevel errorLevel);

        private readonly CampaignGenerator CampaignGen;

        private readonly MissionGenerator Generator;

        private static event LogHandler OnMessageLogged;

        public BriefingRoom(LogHandler logHandler = null)
        {
            using (INIFile ini = new INIFile($"{BRPaths.DATABASE}Common.ini"))
                TARGETED_DCS_WORLD_VERSION = ini.GetValue("Versions", "DCSVersion", "2.7");

            OnMessageLogged += logHandler;
            Database.Instance.Initialize();

            Generator = new MissionGenerator();
            CampaignGen = new CampaignGenerator(Generator);
        }

        public static DatabaseEntryInfo[] GetDatabaseEntriesInfo(DatabaseEntryType entryType, string parameter = "")
        {
            switch (entryType)
            {
                case DatabaseEntryType.Airbase:
                    if (string.IsNullOrEmpty(parameter)) // No parameter, return none
                        return new DatabaseEntryInfo[] { };
                    else // A parameter was provided, return all airbases from specified theater
                        return (from DBEntryAirbase airbase in Database.Instance.GetAllEntries<DBEntryAirbase>() where airbase.Theater == parameter.ToLowerInvariant() select airbase.GetDBEntryInfo()).OrderBy(x => x.Name).ToArray();

                case DatabaseEntryType.Situation:
                    if (string.IsNullOrEmpty(parameter)) // No parameter, return none
                        return new DatabaseEntryInfo[] { };
                    else // A parameter was provided, return all airbases from specified theater
                        return (from DBEntrySituation situation in Database.Instance.GetAllEntries<DBEntrySituation>() where situation.Theater == parameter.ToLowerInvariant() select situation.GetDBEntryInfo()).OrderBy(x => x.Name).ToArray();

                case DatabaseEntryType.Coalition:
                    return (from DBEntryCoalition coalition in Database.Instance.GetAllEntries<DBEntryCoalition>() select coalition.GetDBEntryInfo()).OrderBy(x => x.Name).ToArray();

                case DatabaseEntryType.DCSMod:
                    return (from DBEntryDCSMod dcsMod in Database.Instance.GetAllEntries<DBEntryDCSMod>() select dcsMod.GetDBEntryInfo()).OrderBy(x => x.Name).ToArray();

                case DatabaseEntryType.MissionFeature:
                    return (from DBEntryFeatureMission missionFeature in Database.Instance.GetAllEntries<DBEntryFeatureMission>() select missionFeature.GetDBEntryInfo()).OrderBy(x => x.Name).ToArray();

                case DatabaseEntryType.OptionsMission:
                    return (from DBEntryOptionsMission missionFeature in Database.Instance.GetAllEntries<DBEntryOptionsMission>() select missionFeature.GetDBEntryInfo()).OrderBy(x => x.Name).ToArray();

                case DatabaseEntryType.ObjectiveFeature:
                    return (from DBEntryFeatureObjective objectiveFeature in Database.Instance.GetAllEntries<DBEntryFeatureObjective>() select objectiveFeature.GetDBEntryInfo()).OrderBy(x => x.Name).ToArray();

                case DatabaseEntryType.ObjectivePreset:
                    return (from DBEntryObjectivePreset objectivePreset in Database.Instance.GetAllEntries<DBEntryObjectivePreset>() select objectivePreset.GetDBEntryInfo()).OrderBy(x => x.Name).ToArray();

                case DatabaseEntryType.ObjectiveTarget:
                    return (from DBEntryObjectiveTarget objectiveTarget in Database.Instance.GetAllEntries<DBEntryObjectiveTarget>() select objectiveTarget.GetDBEntryInfo()).OrderBy(x => x.Name).ToArray();

                case DatabaseEntryType.ObjectiveTargetBehavior:
                    return (from DBEntryObjectiveTargetBehavior objectiveTargetBehavior in Database.Instance.GetAllEntries<DBEntryObjectiveTargetBehavior>() select objectiveTargetBehavior.GetDBEntryInfo()).OrderBy(x => x.Name).ToArray();

                case DatabaseEntryType.ObjectiveTask:
                    return (from DBEntryObjectiveTask objectiveTask in Database.Instance.GetAllEntries<DBEntryObjectiveTask>() select objectiveTask.GetDBEntryInfo()).OrderBy(x => x.Name).ToArray();

                case DatabaseEntryType.Theater:
                    return (from DBEntryTheater theater in Database.Instance.GetAllEntries<DBEntryTheater>() select theater.GetDBEntryInfo()).OrderBy(x => x.Name).ToArray();

                case DatabaseEntryType.Unit:
                    return (from DBEntryUnit unit in Database.Instance.GetAllEntries<DBEntryUnit>() select unit.GetDBEntryInfo()).OrderBy(x => x.Name).ToArray();

                case DatabaseEntryType.UnitCarrier:
                    return (from DBEntryUnit unitCarrier in Database.Instance.GetAllEntries<DBEntryUnit>() where Toolbox.CARRIER_FAMILIES.Intersect(unitCarrier.Families).Count() > 0 select unitCarrier.GetDBEntryInfo()).OrderBy(x => x.Name).ToArray();

                case DatabaseEntryType.UnitFlyableAircraft:
                    return (from DBEntryUnit unitFlyable in Database.Instance.GetAllEntries<DBEntryUnit>() where unitFlyable.AircraftData.PlayerControllable select unitFlyable.GetDBEntryInfo()).OrderBy(x => x.Name).ToArray();

                case DatabaseEntryType.WeatherPreset:
                    return (from DBEntryWeatherPreset weatherPreset in Database.Instance.GetAllEntries<DBEntryWeatherPreset>() select weatherPreset.GetDBEntryInfo()).OrderBy(x => x.Name).ToArray();
            }

            return null;
        }

        public static DatabaseEntryInfo? GetSingleDatabaseEntryInfo(DatabaseEntryType entryType, string id)
        {
            // Database entry ID doesn't exist
            if (!GetDatabaseEntriesIDs(entryType).Contains(id)) return null;

            DatabaseEntryInfo[] databaseEntryInfos = GetDatabaseEntriesInfo(entryType);
            return
                (from DatabaseEntryInfo databaseEntryInfo in databaseEntryInfos
                 where databaseEntryInfo.ID.ToLowerInvariant() == id.ToLowerInvariant()
                 select databaseEntryInfo).First();
        }

        public static List<string> GetAircraftLiveries(string aircraftID) =>
            Database.Instance.GetEntry<DBEntryUnit>(aircraftID).AircraftData.Liveries;

        public static List<string> GetAircraftPayloads(string aircraftID) =>
            Database.Instance.GetEntry<DBEntryUnit>(aircraftID).AircraftData.PayloadTasks.Keys.ToList();


        public static string GetAlias(int index) => Toolbox.GetAlias(index);

        public static string FormatPayload(string payload) => Toolbox.FormatPayload(payload);

        public static string[] GetDatabaseEntriesIDs(DatabaseEntryType entryType, string parameter = "")
        {
            return (from DatabaseEntryInfo entryInfo in GetDatabaseEntriesInfo(entryType, parameter) select entryInfo.ID).ToArray();
        }

        public DCSMission GenerateMission(string templateFilePath, bool useObjectivePresets = false)
        {
            return Generator.GenerateRetryable(new MissionTemplate(templateFilePath), useObjectivePresets);
        }

        public DCSMission GenerateMission(MissionTemplate template, bool useObjectivePresets = false)
        {
            return Generator.GenerateRetryable(template, useObjectivePresets);
        }

        public DCSCampaign GenerateCampaign(string templateFilePath, bool useObjectivePresets = false)
        {
            return CampaignGen.Generate(new CampaignTemplate(templateFilePath));
        }

        public DCSCampaign GenerateCampaign(CampaignTemplate template)
        {
            return CampaignGen.Generate(template);
        }

        public static string GetBriefingRoomRootPath() { return BRPaths.ROOT; }

        public static string GetBriefingRoomMarkdownPath() { return BRPaths.INCLUDE_MARKDOWN; }

        public static string GetDCSMissionPath()
        {
            string[] possibleDCSPaths = new string[] { "DCS.earlyaccess", "DCS.openbeta", "DCS" };

            for (int i = 0; i < possibleDCSPaths.Length; i++)
            {
                string dcsPath = Toolbox.PATH_USER + "Saved Games\\" + possibleDCSPaths[i] + "\\Missions\\";
                if (Directory.Exists(dcsPath)) return dcsPath;
            }

            return Toolbox.PATH_USER_DOCS;
        }

        public static string GetDCSCampaignPath()
        {
            string campaignPath = $"{GetDCSMissionPath()}Campaigns\\multilang\\";

            if (Directory.Exists(campaignPath)) return campaignPath;

            return Toolbox.PATH_USER_DOCS;
        }

        internal static void PrintToLog(string message, LogMessageErrorLevel errorLevel = LogMessageErrorLevel.Info)
        {
            OnMessageLogged?.Invoke(message, errorLevel);

            // Throw an exception if there was an error.
            if (errorLevel == LogMessageErrorLevel.Error)
                throw new BriefingRoomException(message);
        }

        public void Dispose()
        {
            Generator.Dispose();
        }
    }
}

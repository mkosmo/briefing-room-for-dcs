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
using BriefingRoom4DCS.Template;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace BriefingRoom4DCS.Generator
{
    internal enum UnitMakerGroupSetting
    {
        AirbaseID,
        AircraftPayload,
        CoordinatesDestination,
        Country,
        FirstUnitIsPlayer,
        Hidden,
        PlayerStartLocation,
        RequiresOpenAirParking,
        RequiresParkingSpots,
    }


    //FirstUnitIsPlayer = 1,
    //    /// <summary>
    //    /// Unit group will be hidden in the planning, on MFD SA pages and on the F10 map.
    //    /// </summary>
    //    Hidden = 2,

    //     = 4,
    //     = 8

    internal class UnitMaker : IDisposable
    {
        private readonly DCSMission Mission;
        private readonly MissionTemplate Template;
        private readonly DBEntryCoalition[] CoalitionsDB;
        private readonly Coalition PlayerCoalition;
        private readonly List<DBEntryTheaterSpawnPoint> SpawnPoints;
        private readonly DBEntryTheater TheaterDB;
        private readonly Country[][] CoalitionsCountries;

        private readonly Dictionary<Country, Dictionary<UnitCategory, List<string>>> UnitLuaTables = new Dictionary<Country, Dictionary<UnitCategory, List<string>>>();

        private int GroupID;

        internal UnitMaker(DCSMission mission, MissionTemplate template, DBEntryCoalition[] coalitionsDB, DBEntryTheater theaterDB, Coalition playerCoalition, Country[][] coalitionsCountries)
        {
            Mission = mission;
            Template = template;

            CoalitionsDB = coalitionsDB;
            PlayerCoalition = playerCoalition;
            TheaterDB = theaterDB;
            SpawnPoints = theaterDB.SpawnPoints.ToList();
            CoalitionsCountries = coalitionsCountries;

            Clear();
        }

        internal void Clear()
        {
            GroupID = 1;
            UnitLuaTables.Clear();
        }

        internal int AddUnitGroup(
            UnitFamily family, int unitCount, Side side,
            string groupLua, string unitLua,
            Coordinates coordinates, DCSSkillLevel skill,
            params KeyValuePair<UnitMakerGroupSetting, int>[] unitGroupSetting)
        {
            DBEntryCoalition unitsCoalitionDB = CoalitionsDB[(int)((side == Side.Ally) ? PlayerCoalition : PlayerCoalition.GetEnemy())];
            
            string[] unitsID = unitsCoalitionDB.GetRandomUnits(family, Template.ContextDecade, unitCount, Template.Mods, true);
            if (unitsID.Length == 0) return 0;

            return AddUnitGroup(unitsID, side, family.GetUnitCategory(), groupLua, unitLua, coordinates, skill, unitGroupSetting);
        }

        internal int AddUnitGroup(
            string[] unitsID, Side side, UnitCategory category,
            string groupLua, string unitLua,
            Coordinates coordinates, DCSSkillLevel skill,
            params KeyValuePair<UnitMakerGroupSetting, int>[] unitGroupSetting)
        {
            Country country = ((side == Side.Ally) ? PlayerCoalition : PlayerCoalition.GetEnemy()) == Coalition.Blue ? Country.CJTFBlue : Country.CJTFRed;

            string lua = File.ReadAllText($"{BRPaths.INCLUDE_LUA_UNITS}{Toolbox.AddMissingFileExtension(groupLua, ".lua")}");
            LuaTools.ReplaceKey(ref lua, "ID", GroupID);
            LuaTools.ReplaceKey(ref lua, "X", coordinates.X);
            LuaTools.ReplaceKey(ref lua, "Y", coordinates.Y);

            AddUnitGroupToTable(country, category, lua);

            GroupID++;
            return GroupID - 1;
        }

        private void AddUnitGroupToTable(Country country, UnitCategory category, string unitGroupLua)
        {
            if (!UnitLuaTables.ContainsKey(country)) UnitLuaTables.Add(country, new Dictionary<UnitCategory, List<string>>());
            if (!UnitLuaTables[country].ContainsKey(category)) UnitLuaTables[country].Add(category, new List<string>());
            UnitLuaTables[country][category].Add(unitGroupLua);
        }

        internal string GetUnitsLuaTable(Coalition coalition)
        {
            string unitsLuaTable = "";

            for (int countryIndex = 0; countryIndex < CoalitionsCountries[(int)coalition].Length; countryIndex++) // Check all countries in this coalition
            {
                Country country = CoalitionsCountries[(int)coalition][countryIndex];

                if (!UnitLuaTables.ContainsKey(country)) continue; // No units for this country

                unitsLuaTable += $"[{countryIndex + 1}] =\n";
                unitsLuaTable += "{\n";
                unitsLuaTable += $"[\"id\"] = {(int)country},\n";

                foreach (UnitCategory unitCategory in Toolbox.GetEnumValues<UnitCategory>()) // Check all coalitions
                {
                    if (!UnitLuaTables[country].ContainsKey(unitCategory)) continue; // No unit for this unit category

                    unitsLuaTable += $"[\"{unitCategory.ToString().ToLowerInvariant()}\"] =\n";
                    unitsLuaTable += "{\n";

                    for (int groupIndex = 0; groupIndex < UnitLuaTables[country][unitCategory].Count; groupIndex++)
                    {
                        unitsLuaTable += $"[{groupIndex + 1}] =\n";
                        unitsLuaTable += "{\n";
                        unitsLuaTable += $"{UnitLuaTables[country][unitCategory][groupIndex]}\n";
                        unitsLuaTable += "},\n";
                    }

                    unitsLuaTable += "},\n";
                }

                unitsLuaTable += "},\n";
            }

            return unitsLuaTable;
        }

        //    internal int AddUnitGroup(
        //string[] units, Side side,
        //string groupLua, string unitLua,
        //Coordinates coordinates, Coordinates? coordinates2,
        //DCSSkillLevel skill, DCSMissionUnitGroupFlags flags = 0, AircraftPayload payload = AircraftPayload.Default,
        //int airbaseID = 0, Country? country = null, PlayerStartLocation startLocation = PlayerStartLocation.Runway)
        //    {


        //        GroupID++;
        //        return GroupID - 1;
        //    }

        //    internal int AddUnitGroup(
        //string[] units, Side side,
        //string groupLua, string unitLua,
        //Coordinates coordinates, Coordinates? coordinates2,
        //DCSSkillLevel skill, DCSMissionUnitGroupFlags flags = 0, AircraftPayload payload = AircraftPayload.Default,
        //int airbaseID = 0, Country? country = null, PlayerStartLocation startLocation = PlayerStartLocation.Runway)
        //    {


        //        GroupID++;
        //        return GroupID - 1;
        //    }

        /*
         * public void MyFunction(params KeyValuePair<string, object>[] pairs)
{
    // ...
}*/

        //internal int AddUnitGroup(
        //    string[] units, Side side,
        //    string groupLua, string unitLua,
        //    Coordinates coordinates, Coordinates? coordinates2,
        //    DCSSkillLevel skill, DCSMissionUnitGroupFlags flags = 0, AircraftPayload payload = AircraftPayload.Default,
        //    int airbaseID = 0, Country? country = null, PlayerStartLocation startLocation = PlayerStartLocation.Runway)
        //{


        //    GroupID++;
        //    return GroupID - 1;
        //}

        public void Dispose()
        {

        }
    }
}

//namespace BriefingRoom4DCS.Generator
//{
//    internal class UnitMaker : IDisposable
//    {
//        private const double AIRCRAFT_UNIT_SPACING = 50.0;

//        private const double SHIP_UNIT_SPACING = 100.0;

//        private const double VEHICLE_UNIT_SPACING = 20.0;

//        private int NextGroupID;
//        private int NextUnitID;

//        internal UnitMakerSpawnPointSelector SpawnPointSelector { get; }
//        internal UnitMakerCallsignGenerator CallsignGenerator { get; }

//        internal UnitMaker(DBEntryCoalition[] coalitionsDB, DBEntryTheater theaterDB)
//        {
//            CallsignGenerator = new UnitMakerCallsignGenerator(coalitionsDB);
//            SpawnPointSelector = new UnitMakerSpawnPointSelector(theaterDB);

//            NextGroupID = 1;
//            NextUnitID = 1;
//        }

//        internal DCSMissionUnitGroup AddUnitGroup(
//            DCSMission mission, string[] units, Side side,
//            Coordinates coordinates, string groupLua, string unitLua,
//            DCSSkillLevel skill, DCSMissionUnitGroupFlags flags = 0, AircraftPayload payload = AircraftPayload.Default,
//            Coordinates? coordinates2 = null, int airbaseID = 0, bool requiresParkingSpots = false, bool requiresOpenAirParking = false, Country? country = null, PlayerStartLocation startLocation = PlayerStartLocation.Runway)
//        {
//            if (units.Length == 0) return null; // No units database entries ID provided, cancel group creation

//            // TODO: check for missing units
//            DBEntryUnit[] unitsBP = (from string u in units where Database.Instance.EntryExists<DBEntryUnit>(u) select Database.Instance.GetEntry<DBEntryUnit>(u)).ToArray();
//            unitsBP = (from DBEntryUnit u in unitsBP where u != null select u).ToArray();
//            if (unitsBP.Length == 0) return null; // All database entries were null, cancel group creation

//            Coalition coalition = (side == Side.Ally) ? mission.CoalitionPlayer : mission.CoalitionEnemy; // Pick group coalition
//            if(!country.HasValue)
//                country = coalition == Coalition.Blue? Country.CJTFBlue : Country.CJTFRed;

//            double groupHeading = unitsBP[0].IsAircraft ? 0 : Toolbox.RandomDouble(Toolbox.TWO_PI); // Generate global group heading

//            // Generate units in the group
//            int unitIndex = 0;
//            Coordinates? lastSpot = null;
//            List<DCSMissionUnitGroupUnit> groupUnits = new List<DCSMissionUnitGroupUnit>();
//            foreach (DBEntryUnit unitBP in unitsBP)
//            {
//                if (unitBP == null) continue;

//                for (int i = 0; i < unitBP.DCSIDs.Length; i++)
//                {
//                    // Set unit coordinates and heading
//                    Coordinates unitCoordinates = coordinates;
//                    double unitHeading = groupHeading;

//                    SetUnitCoordinatesAndHeading(ref unitCoordinates, ref unitHeading, unitBP, unitIndex);

//                    // Get parking spot for the unit, if unit is parked at an airdrome
//                    int parkingSpot = 0;
//                    if (airbaseID > 0)
//                    {
//                        if (requiresParkingSpots)
//                        {
//                            parkingSpot = SpawnPointSelector.GetFreeParkingSpot(airbaseID, lastSpot, out Coordinates parkingCoordinates, requiresOpenAirParking);
//                            if (parkingSpot >= 0)
//                               unitCoordinates = parkingCoordinates;
//                            else
//                               parkingSpot = 0;
//                            lastSpot = unitCoordinates;
//                        }
//                    } else if(airbaseID == -99) //carrier code always parks 1 maybe will need more
//                        parkingSpot = 1;
//                    // Add unit to the list of units
//                    DCSMissionUnitGroupUnit unit = new DCSMissionUnitGroupUnit
//                    {
//                        Coordinates = unitCoordinates,
//                        Heading = unitHeading,
//                        ID = NextUnitID,
//                        Type = unitBP.DCSIDs[i],
//                        ParkingSpot = parkingSpot,
//                        Name = unitBP.ID
//                    };
//                    groupUnits.Add(unit);
//                    unitIndex++; NextUnitID++;
//                }
//            }

//            // Generate group name
//            string groupName;
//            UnitCallsign callsign = new UnitCallsign();
//            if (unitsBP[0].IsAircraft) // Aircraft group, name is a callsign
//            {
//                callsign = CallsignGenerator.GetCallsign(unitsBP[0].Families[0], coalition);
//                groupName = callsign.GroupName;
//            }
//            else // Vehicle/ship/static group, name is a random group name
//                groupName = GetGroupName(unitsBP[0].Families[0]);

//            // Add group to the mission
//            DCSMissionUnitGroup group = new DCSMissionUnitGroup
//            {
//                AirbaseID = airbaseID,
//                CallsignLua = callsign.Lua,
//                Category = unitsBP[0].Category,
//                Coalition = coalition,
//                Country = country.Value,
//                Coordinates = airbaseID != 0?  groupUnits[0].Coordinates : coordinates,
//                Coordinates2 = coordinates2 ?? coordinates + Coordinates.CreateRandom(1, 2) * Toolbox.NM_TO_METERS,
//                Flags = flags,
//                GroupID = NextGroupID,
//                LuaGroup = groupLua,
//                Name = groupName,
//                Skill = skill,
//                Payload = payload,
//                UnitID = units[0],
//                LuaUnit = unitLua,
//                Units = groupUnits.ToArray(),
//                StartLocation = startLocation
//            };
//            mission.UnitGroups.Add(group);

//            NextGroupID++;

//            BriefingRoom.PrintToLog($"Added \"{group.Units[0].Type}\" unit group \"{group.Name}\" for coalition {group.Coalition.ToString().ToUpperInvariant()}");

//            return group;
//        }

//        private string GetGroupName(UnitFamily family)
//        {
//            string name = GeneratorTools.ParseRandomString(Database.Instance.Common.UnitGroupNames[(int)family]);

//            int fakeGroupNumber = NextGroupID * 10 + Toolbox.RandomInt(1, 10);
//            name = name.Replace("$N$", fakeGroupNumber.ToString(NumberFormatInfo.InvariantInfo));
//            name = name.Replace("$NTH$", Toolbox.GetOrdinalAdjective(fakeGroupNumber));
//            return name;
//        }

//        private void SetUnitCoordinatesAndHeading(ref Coordinates unitCoordinates, ref double unitHeading, DBEntryUnit unitBP, int unitIndex)
//        {
//            if (unitBP.IsAircraft)
//                unitCoordinates += new Coordinates(AIRCRAFT_UNIT_SPACING, AIRCRAFT_UNIT_SPACING) * unitIndex;
//            else
//            {
//                if (unitBP.OffsetCoordinates.Length > unitIndex) // Unit has a fixed set of coordinates (for SAM sites, etc.)
//                {
//                    double s = Math.Sin(unitHeading);
//                    double c = Math.Cos(unitHeading);
//                    Coordinates offsetCoordinates= unitBP.OffsetCoordinates[unitIndex];
//                    unitCoordinates += new Coordinates( offsetCoordinates.X * c - offsetCoordinates.Y * s, offsetCoordinates.X * s + offsetCoordinates.Y * c);
//                }
//                else // No fixed coordinates, generate random coordinates
//                {
//                    switch (unitBP.Category)
//                    {
//                        case UnitCategory.Ship:
//                            unitCoordinates = unitCoordinates.CreateNearRandom(SHIP_UNIT_SPACING, SHIP_UNIT_SPACING * 10);
//                            break;
//                        case UnitCategory.Static: // Static units are spawned exactly on the group location (and there's only a single unit per group)
//                            break;
//                        default:
//                            unitCoordinates = unitCoordinates.CreateNearRandom(VEHICLE_UNIT_SPACING, VEHICLE_UNIT_SPACING * 10);
//                            break;
//                    }
//                }

//                if (unitBP.OffsetHeading.Length > unitIndex) // Unit has a fixed heading (for SAM sites, etc.)
//                    unitHeading = Toolbox.ClampAngle(unitHeading + unitBP.OffsetHeading[unitIndex]); // editor looks odd but works fine if negative or over 2Pi
//                else if(unitBP.Category != UnitCategory.Ship)
//                    unitHeading = Toolbox.RandomDouble(Toolbox.TWO_PI);
//            }
//        }

//        /// <summary>
//        /// <see cref="IDisposable"/> implementation.
//        /// </summary>
//        public void Dispose() { }
//    }
//}

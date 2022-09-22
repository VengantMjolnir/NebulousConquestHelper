﻿using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Utility;

namespace NebulousConquestHelper
{
    [XmlType("ConquestGame")]
    [Serializable]
    public class GameInfo
    {
        public enum ConquestTeam
        {
            GreenTeam,
            OrangeTeam
        }

        public string ScenarioName;
        public int CurrentTurn;
        public List<TeamInfo> Teams;
        public List<FleetInfo> Fleets;
        public SystemInfo System;

        public static bool Init(object loaded)
        {
            GameInfo game = (GameInfo)loaded;

            if (game == null) return false;

            game.System.InitSystem();
            foreach (LocationInfo loc in game.System.AllLocations)
            {
                loc.PresentFleets = new List<FleetInfo>();
            }

            foreach (FleetInfo fleet in game.Fleets)
            {
                fleet.Fleet = (SerializedConquestFleet)Helper.ReadXMLFile(
                    typeof(SerializedConquestFleet),
                    new FilePath(Helper.DATA_FOLDER_PATH + fleet.FleetFileName + Helper.FLEET_FILE_TYPE)
                );

                if (fleet.Fleet == null) return false;

                fleet.Location = game.System.FindLocationByName(fleet.LocationName);

                if (fleet.Location == null) return false;

                fleet.Location.PresentFleets.Add(fleet);
            }

            foreach(var team in game.Teams)
            {
                team.PostLoadInit();
            }

            return true;
        }

        public void CreateNewFleet(string fleetFileName, string locationName)
        {
            FleetInfo newFleet = new FleetInfo(fleetFileName, locationName);

            newFleet.Location = System.FindLocationByName(locationName);
            newFleet.Location.PresentFleets.Add(newFleet);

            Fleets.Add(newFleet);
        }

        public void SaveGame()
        {
            foreach (FleetInfo fleet in Fleets)
            {
                fleet.SaveFleet();
            }

            Helper.WriteXMLFile(typeof(GameInfo), new FilePath(Helper.DATA_FOLDER_PATH + "TestGame.conquest"), this);
        }

        public TeamInfo GetTeam(ConquestTeam team)
        {
            return team == ConquestTeam.GreenTeam ? Teams[0] : Teams[1];
        }
    }
}

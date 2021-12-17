using Oxide.Core.Plugins;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PrefabClock", "bmgjet", "1.0.1")]
    [Description("Controls bmgjets clock prefab")]
    public class PrefabClock : RustPlugin
    {
        //Prefab name layouts
        //Example
        //BMGJETCLOCK24.S.00.00.00.15.00.00.60.{CRED}Light{CEND} And {CBLUE}Tesla Coil{CEND} @ {P} has {1} for {T} secs

        //Break Down
        //BMGJETCLOCK to trigger plugin on prefab name
        //24 or 12 to set desplay type.
        //full stop "." to seperate next settings.

        //S = Games time
        //R = Real time
        //Add a C to make it a count down so like SC or RC.

        //Year to trigger on example 2021
        //Month to trigger on Example 11
        //Day to trigger on example 30
        //Use 00.00.00 to make it trigger daily

        //Delay before it deactivates. Example shown is 60 seconds. Use 00 to make it never disable. If using daily setting will reset at midnight.

        //Lastly text that will be used in the announcement messages if they are enabled.
        //Leave it blank for no annoucements on that clocks trigger.
        //REGEX is used to set up custom things.
        //Such as the example {CRED} would replace that with <color=red>
        //{P} outputs the grid position
        //{T} outputs the delay before auto disable
        //{1} selects what state text to use from Dictionary
        //These can be changed or added to in the plugin below

        Dictionary<string, Dictionary<string, string>> CustomText = new Dictionary<string, Dictionary<string, string>>()
        {
        //ID used                                      Message when triggered          Message when disabled
        {"{1}", new Dictionary<string, string>(){{"<color=green>Enabled</color>", "<color=red>Disabled</color>"}}},
        {"{2}", new Dictionary<string, string>(){{"<color=green>Opened</color>", "<color=red>Closed</color>"}}},
        };

        //Custom Tags
        Dictionary<string, string> CustomTags = new Dictionary<string, string>()
        {
        //ID       Replace with
        {"{CEND}", "</color>"},
        {"{CRED}", "<color=red>"},
        {"{CBLUE}","<color=blue>"},
        {"{CBLACK}","<color=black>"},
        {"{CORANGE}","<color=orange>"},
        };

        #region Vars
        //Permission to see IO Output
        const string SeeIOOutput = "PrefabClock.See";

        //Settings
        //TimeZoneOffset
        public int D = 0; //Days
        public int H = 0; //Hours
        public int M = 0; //Mins
        public int S = 0; //Secs

        //IO Output
        public bool EnableOutput = true;  //Provides date/time based output that triggers
        public bool Announcements = true; //Announces in Chat when somethings triggered
        public string SteamIcon = "76561197980831914"; //Get Icon from this profile for use in announcements
        //End Settings

        //Holds info collected from MAP.
        Dictionary<Vector3, string> ClockSettings = new Dictionary<Vector3, string>();
        //Reference for componant
        private static PrefabClock plugin;

        //Reference Vanish plugin since can cause issues when players are in vanish and moving them.
        [PluginReference]
        private Plugin Vanish;
        #endregion

        #region Plugin Core
        private void Init()
        {
            //Set up permission that allows seeing clock blocker IO
            permission.RegisterPermission(SeeIOOutput, this);
        }

        //Hides spawned ElectricalBlockers that are used as IO point
        object CanNetworkTo(ElectricalBlocker EB, BasePlayer player)
        {
            if (EB == null || player == null) return null;
            //ID it by ownerid and skinid to return fast on other ElectricalBlocker
            if (EB.OwnerID != 0 && EB.skinID != 264592) return null;
            //If player has permission allow them to see clock blocker IO
            if (!player.IPlayer.HasPermission(SeeIOOutput))
            {
                //Return anything other than null to make it invisiable
                return false;
            }
            //Normal behavour
            return null;
        }

        private void OnServerInitialized(bool initial)
        {
            //Delay startup if fresh server boot. Helps hooking on slow servers
            if (initial)
            {
                Fstartup();
                return;
            }
            //Startup plugin
            Startup();
        }

        private void Fstartup()
        {
            //Waits for player before running script to help first startup performance.
            timer.Once(10f, () =>
            {
                if (Rust.Application.isLoading)
                {
                    //No players so run a timer again in 10 sec to check.
                    Fstartup();
                    return;
                }
                //Starup script now.
                Startup();
            });
        }

        private void Startup()
        {
            //Int to keep track of added scripts
            int Clocks = 0;
            //Clears clocksettings incase its triggered reload.
            ClockSettings.Clear();
            //Add reference to plugin for scripts to use.
            plugin = this;
            //Find All counters on the map
            for (int i = World.Serialization.world.prefabs.Count - 1; i >= 0; i--)
            {
                PrefabData prefabdata = World.Serialization.world.prefabs[i];
                //Check the prefab datas category since thats where customprefabs names are stored
                if (prefabdata.id == 4254177840 && prefabdata.category.Contains("BMGJETCLOCK"))
                {
                    //Scan found counters
                    PowerCounter PC = FindCounter(prefabdata.position, 0.1f, Color.blue);
                    string settings = prefabdata.category.Split(':')[1].Replace("\\", "");

                    //Do nothing since already a script there or no couter at that position.
                    if (PC == null || PC.GetComponent<PrefabClockAddon>() != null) return;
                    //Keep track of prefab position and settings
                    ClockSettings.Add(PC.transform.position, settings);

                    //Power up counter so it displays numbers
                    PC.SetFlag(PowerCounter.Flag_HasPower, true);
                    //Disable showing power its passing to show its target
                    PC.SetFlag(PowerCounter.Flag_ShowPassthrough, false);
                    PC.SetFlag(IOEntity.Flags.Reserved8,true);
                    PC.SetFlag(IOEntity.Flag_HasPower, true);
                    //Add clock script
                    PC.gameObject.AddComponent<PrefabClockAddon>();
                    PC.SendNetworkUpdateImmediate();
                    //Add another script to the counter.
                    Clocks++;
                }
            }
            //Outputs debug info
            Puts("Running " + Clocks.ToString() + " clock scripts");
        }

        private void Unload()
        {
            //Remove static reference to self
            plugin = null;
            //Remove scripts incase its a plugin restart and not server restart which would destroy them
            foreach (var ClockScripts in GameObject.FindObjectsOfType<PowerCounter>())
            {
                foreach (var cs in ClockScripts.GetComponentsInChildren<PrefabClockAddon>())
                {
                    //Destroy the script
                    UnityEngine.Object.DestroyImmediate(cs);
                }
            }
        }

        PowerCounter FindCounter(Vector3 pos, float radius, Color c)
        {
            //Debug shows where its scanning for admins with see permission
            foreach (BasePlayer BP in BasePlayer.activePlayerList)
            {
                if (BP.IsAdmin && BP.IPlayer.HasPermission(SeeIOOutput)) BP.SendConsoleCommand("ddraw.sphere", 8f, c, pos, radius);
            }
            //Scans area for counters
            List<PowerCounter> Counters = new List<PowerCounter>();
            Vis.Entities<PowerCounter>(pos, radius, Counters);
            foreach (PowerCounter PC in Counters)
            {
                //Returns first found one
                return PC;
            }
            return null;
        }

        ElectricalBlocker FindSocket(Vector3 pos, float radius)
        {
            //Debug shows where its scanning for admins with see permission
            foreach (BasePlayer BP in BasePlayer.activePlayerList)
            {
                if (BP.IsAdmin && BP.IPlayer.HasPermission(SeeIOOutput)) BP.SendConsoleCommand("ddraw.sphere", 8f, Color.green, pos, radius);
            }
            //Scan for electrical blockers which are used a IO points
            List<ElectricalBlocker> Counters = new List<ElectricalBlocker>();
            Vis.Entities<ElectricalBlocker>(pos, radius, Counters);
            foreach (ElectricalBlocker FS in Counters)
            {
                //retruns first one found
                return FS;
            }
            return null;
        }

        //Loop that follows the output path on IO
        void FollowIOPath(IOEntity FollowIOPath, bool Enable)
        {
            while (FollowIOPath != null)
            {
                //Keep looping until the end of the path.
                FollowIOPath = plugin.ToggleIO(FollowIOPath, Enable);
            }
        }

        //Logic behind toggling the IO since we arnt using power unless first connect item is a orswitch
        IOEntity ToggleIO(IOEntity FollowIOPath, bool enabled)
        {
            //Enable it
            FollowIOPath.SetFlag(BaseEntity.Flags.Reserved8, enabled);
            FollowIOPath.SetFlag(IOEntity.Flag_HasPower, enabled);
            FollowIOPath.SetFlag(BaseEntity.Flags.On, enabled);

            //Cast as Igniter to do some extra functions if its a Igniter
            Igniter igniter = FollowIOPath as Igniter;
            if (igniter != null)
            {
                //Remove it from destroying itself
                igniter.SelfDamagePerIgnite = 0f;
                if (enabled)
                {
                    //Triggers its input as having 100 power.
                    igniter.UpdateHasPower(100, 0);
                }
                else
                {
                    //Triggers its input as having 0 power so it switches off.
                    igniter.UpdateHasPower(0, 0);
                }
                //End loop so theres no outputs from an ignitor
                return null;
            }

            //Cast as Doormanipulator to do some extra functions if its a Doormanipulator
            DoorManipulator DoorMan = FollowIOPath as DoorManipulator;
            if (DoorMan != null)
            {
                DoorMan.SetFlag(DoorManipulator.Flags.Open, enabled);
                //Triggers the action
                DoorMan.DoAction();
                //Exit function early to improve performance on long door strings
                DoorMan.SendNetworkUpdateImmediate();
                return DoorMan.outputs[0].connectedTo.ioEnt;
            }
            //Handle OrSwitch Use a Orswitch as a power output.
            ORSwitch OrSwitch = FollowIOPath as ORSwitch;
            if (OrSwitch != null)
            {
                if (enabled)
                {
                    //Makes it output 999 power
                    OrSwitch.UpdateFromInput(999, 0);
                }
                else
                {
                    //Removes all its power.
                    OrSwitch.UpdateFromInput(0, 0);
                }
                //Exit early and dont follow the path since real power will trigger the rest of the stuff.
                OrSwitch.UpdateOutputs();
                OrSwitch.SendNetworkUpdateImmediate();
                return null;
            }
            //Handle Teslacoil if using timer on one as a trap.
            TeslaCoil Tcoil = FollowIOPath as TeslaCoil;
            if (Tcoil != null)
            {
                Tcoil.SetFlag(TeslaCoil.Flag_StrongShorting, enabled);
                //Need to do something about a damage setting
                Tcoil.maxDischargeSelfDamageSeconds = 120f;
                if (enabled)
                {
                    //Trigger teslacoils function
                    Tcoil.InvokeRepeating(Tcoil.Discharge, 1, 0.50f);
                }
                else
                {
                    //Stops teslacoils function
                    Tcoil.CancelInvoke(Tcoil.Discharge);
                }
                return null;
            }
            //Send update to clients
            FollowIOPath.SendNetworkUpdateImmediate();
            //Return the next IO in the connection
            return FollowIOPath.outputs[0].connectedTo.ioEnt;
        }

        //Sends message to all active players under a steamID
        void CreateAnouncment(string msg, bool open, Vector3 pos, int time = 0)
        {
            //Parse out text tags with regex
            // {P} give position on grid
            msg = msg.Replace("{P}", getGrid(pos));
            // {T} give the ammount of time before auto close
            if (time != 0)
                msg = msg.Replace("{T}", time.ToString());
            //Find outher tags with in { } brackets
            MatchCollection matches = Regex.Matches(msg, "{.*?}");
            foreach (Match match in matches)
            {
                //Tags to replace things you cant enter in the prefab name like color codes.
                if (CustomTags.ContainsKey(match.ToString()))
                {
                    msg = msg.Replace(match.ToString(), CustomTags[match.ToString()]);
                    continue;
                }

                //Check if the tag exsists in the CustomText Dictornaray.
                if (CustomText.ContainsKey(match.ToString()))
                {
                    if (open)
                    {
                        //Send Triggered Message
                        msg = msg.Replace(match.ToString(), CustomText[match.ToString()].ElementAt(0).Key);
                    }
                    else
                    {
                        //Send untriggered message
                        msg = msg.Replace(match.ToString(), CustomText[match.ToString()].ElementAt(0).Value);
                    }
                }
            }
            //Loop though each player on the server. Cast to array incase theres a player joining or leaving as messages are getting sent.
            foreach (BasePlayer current in BasePlayer.activePlayerList.ToArray())
            {
                if (current.IsConnected)
                {
                    //Send message as a user so can use there steam picture
                    rust.SendChatMessage(current, "", msg, SteamIcon);
                }
            }
        }

        //Gets grid letter from world position
        string getGrid(Vector3 pos)
        {
            //Set base letter
            char letter = 'A';
            var x = Mathf.Floor((pos.x + (ConVar.Server.worldsize / 2)) / 146.3f) % 26;
            var z = (Mathf.Floor(ConVar.Server.worldsize / 146.3f)) - Mathf.Floor((pos.z + (ConVar.Server.worldsize / 2)) / 146.3f);
            letter = (char)(((int)letter) + x);
            //-1 since starts at 0
            return $"{letter}{z - 1}";
        }

        //Resets the ability for the admin to see the IO on the Clocks
        public void ResetCanNetwork(BasePlayer player)
        {
            //Holds Orignal position
            Vector3 OP = player.transform.position;
            //Create a platform to stand on temporally
            BuildingBlock entity = GameManager.server.CreateEntity("assets/prefabs/building core/floor/floor.prefab", RandomLocation(), player.transform.rotation) as BuildingBlock;
            if (entity == null) return;
            entity.Spawn();
            //Grounded so doesnt fall apart
            entity.grounded = true;
            //Attach it to a new ID
            entity.AttachToBuilding(BuildingManager.server.NewBuildingID());
            //Set its health as max for its building grade
            entity.health = entity.MaxHealth();
            //Update clients
            entity.SendNetworkUpdateImmediate();
            //Teleports player to platform
            Tele(player, entity.transform.position);
            player.ChatMessage("Please wait 10 sec then youll be TP backed");
            //Delay to allow stuff to load out.
            timer.Once(10f, () =>
            {
                Tele(player, OP);
                //Remove the temp platform.
                entity.Kill();
            });
        }

        private Vector3 RandomLocation()
        {
            //Gets the world size. Grid coverage is half the actualy world size
            uint RanMapSize = (uint)(World.Size / 1.5);
            if (RanMapSize >= 4000)
            {
                RanMapSize = 3900; //Limits player from going past 4000 kill point
            }
            //Seeds random function with number from datetime
            System.Random rnd = new System.Random(DateTime.Now.Millisecond);
            //Pick random location on the map at height 820-943 to not interfere with players.
            return new Vector3(rnd.Next(Math.Abs((int)RanMapSize) * (-1), (int)RanMapSize), rnd.Next(820, 943), rnd.Next(Math.Abs((int)RanMapSize) * (-1), ((int)RanMapSize)));
        }

        public void Tele(BasePlayer player, Vector3 Pos)
        {
            if (!player.IsValid()) return;
            try
            {
                //Stops players actions
                player.SetParent(null, true, true);
                player.EndLooting();
                player.RemoveFromTriggers();
                //Disables fall damage
                player.SetServerFall(true);
                //Moves the player
                player.Teleport(Pos);
                if (player.IsConnected)
                {
                    //Puts player into loading screen
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
                    player.ClientRPCPlayer(null, player, "StartLoading");
                    //Sends new entity list to player
                    player.SendEntityUpdate();
                    if (!IsInvisible(player)) // fix for becoming networked briefly with vanish while teleporting
                    {
                        player.UpdateNetworkGroup(); // 1.1.1 building fix @ctv
                        player.SendNetworkUpdateImmediate(false);
                    }
                }
            }
            finally
            {
                //Remove fall protection and updates triggers
                player.ForceUpdateTriggers();
                player.SetServerFall(false);
            }
        }

        //Hook vanish plugin
        bool IsInvisible(BasePlayer player)
        {
            return Vanish != null && Vanish.Call<bool>("IsInvisible", player);
        }

        int CheckMe(BaseEntity be)
        {
            //Work out which counter it is
            Vector3 Left = be.transform.position + be.transform.right * -0.2f;
            Vector3 Right = be.transform.position + be.transform.right * 0.2f;
            var CheckLeft = plugin.FindCounter(Left, 0.01f, Color.red);
            var CheckRight = plugin.FindCounter(Right, 0.01f, Color.red);
            if (CheckLeft && CheckRight) return 1;
            if (CheckLeft && !CheckRight) return 0;
            if (!CheckLeft && CheckRight) return 2;
            return -1;
        }

        //Create IO point on the middle counter
        ElectricalBlocker AddOutput(PowerCounter Mins)
        {
            //Adjust position
            Vector3 pos = Mins.transform.position;
            pos -= Mins.transform.forward * 0.058f;
            pos += Mins.transform.up * 0.168f;
            ElectricalBlocker EB = plugin.FindSocket(pos, 0.25f);
            //Check if outputs is enabled other wise destroy if ones has been created there.
            if (!plugin.EnableOutput)
            {
                if (EB == null)
                {
                    return null;
                }
                EB.Kill();
                return null;
            }
            //If no blocker found create one.
            if (EB == null)
            {
                ElectricalBlocker OutputSocket = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/gates/blocker/electrical.blocker.deployed.prefab", Mins.transform.position, Mins.transform.rotation) as ElectricalBlocker;
                if (OutputSocket == null) return null;
                plugin.DestroyGroundComp(OutputSocket);
                plugin.DestroyMeshCollider(OutputSocket);
                OutputSocket.transform.position = pos;
                //How its identified
                OutputSocket.OwnerID = 0;
                OutputSocket.skinID = 264592;
                //create
                OutputSocket.Spawn();
                //Reference
                return EB;
            }
            else
            {
                //Link to a blocker if already present since must be server restart.
                plugin.DestroyGroundComp(EB);
                plugin.DestroyMeshCollider(EB);
                //How its identified
                EB.OwnerID = 0;
                EB.skinID = 264592;
                //Reference
                return EB;
            }
        }

        void DestroyGroundComp(BaseEntity ent)
        {
            //Stops things breaking from not being grounded
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<DestroyOnGroundMissing>());
            //Stops the checking of being grounded
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<GroundWatch>());
            //Stops Decay
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<DeployableDecay>());
        }

        void DestroyMeshCollider(BaseEntity ent)
        {
            foreach (var mesh in ent.GetComponentsInChildren<MeshCollider>())
            {
                //Removes the collider doesnt interfere since its already in a prefabed invisiable collider to stop players messing with counters settings.
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        //Uses the fact that having outputs disabled removes all spawned ones so toggles that off and on restarting the plugin.
        [ChatCommand("clockreset")]
        private void CRestart(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin)
            {
                player.ChatMessage("Unloading Plugin");
                EnableOutput = !EnableOutput;
                Unload();
                player.ChatMessage("Resetting Plugin");
                Startup();
                EnableOutput = !EnableOutput;
                Unload();
                player.ChatMessage("Restarting Plugin");
                Startup();
            }
        }
        [ChatCommand("clockreload")]
        private void CReload(BasePlayer player, string command, string[] args)
        {
            //Restarts the plugin
            if (player.IsAdmin)
            {
                Unload();
                player.ChatMessage("Reloading Plugin");
                Startup();
            }
        }

        [ChatCommand("clockview")]
        private void CN2U(BasePlayer player, string command, string[] args)
        {
            //Gives admin ability to see Clock Blockers IO
            if (player.IsAdmin && args.Length == 1)
            {
                if (args[0] == "true")
                {
                    player.ChatMessage("Adding View Permission");
                    //Give permission
                    permission.GrantUserPermission(player.UserIDString, "PrefabClock.See", this);
                    timer.Once(1f, () =>
                    {
                        //small delay to permissions to take effect.
                        ResetCanNetwork(player);
                    });
                    return;
                }
                player.ChatMessage("Removing View Permission");
                //Remove permission
                permission.RevokeUserPermission(player.UserIDString, "PrefabClock.See");
                timer.Once(1f, () =>
                {
                    //small delay to permissions to take effect.
                    ResetCanNetwork(player);
                });
            }
        }
        #endregion

        #region Clock Scripts
        private class PrefabClockAddon : MonoBehaviour
        {
            //Show as a countdown to trigger
            bool Countdown = false;
            //Use script on the 3 counters.
            private PowerCounter Hours;
            private PowerCounter Mins;
            private PowerCounter Sec;
            private ElectricalBlocker Output;
            //Get game time based on sky.
            TOD_Sky Sky = TOD_Sky.Instance;
            //24 Hour Clock
            private bool hour24 = false;
            //Check if using the real time
            private bool RealTime = false;
            //Check if should trigger daily
            private bool Daily = false;
            //Trigger Date
            DateTime TriggerDate = new DateTime(2021, 11, 26, 12, 00, 00);
            //How long to wait before closing
            private int CloseDelay = 10;
            //Custom Text
            string CustomText = "";
            //Track of if its already trigged
            private bool triggered = false;

            private void Awake()
            {
                PowerCounter ThisCounter = GetComponent<PowerCounter>();
                //Sets up clocks settings from its custom prefab name
                if (plugin.ClockSettings.ContainsKey(ThisCounter.transform.position))
                {
                    //Reloads settings based on this scripts position.
                    string settings = plugin.ClockSettings[ThisCounter.transform.position];
                    //Settings are seperated by a fullstop
                    string[] ParsedSettings = settings.Split('.');
                    //Load type of clock
                    if (ParsedSettings[0].Contains("CLOCK24"))
                    {
                        hour24 = true;
                    }
                    else if (ParsedSettings[0].Contains("CLOCK12"))
                    {
                        hour24 = false;
                    }
                    else
                    {
                        //Warn that there is invalid setting
                        plugin.Puts("Invalid CLOCK12 or CLOCK24 Name @ " + plugin.getGrid(ThisCounter.transform.position));
                        return;
                    }
                    try
                    {
                        //Only changes to real clock if a R is set
                        switch (ParsedSettings[1])
                        {
                            case "R": //Real time
                                RealTime = true;
                                Countdown = false;
                                break;
                            case "S": //Games time
                                RealTime = false;
                                Countdown = false;
                                break;
                            case "RC": //Real Time count down to trigger
                                RealTime = true;
                                Countdown = true;
                                break;
                            case "SC": //Game time count down to trigger
                                RealTime = false;
                                Countdown = true;
                                break;
                        }
                    }
                    catch
                    {
                        //Warn that there is invalid setting
                        plugin.Puts("Unable to parse R or S from clock prefab @ " + plugin.getGrid(ThisCounter.transform.position));
                        return;
                    }
                    try
                    {
                        //Loads date trigger
                        int year = int.Parse(ParsedSettings[2]);
                        int month = int.Parse(ParsedSettings[3]);
                        int day = int.Parse(ParsedSettings[4]);
                        int hour = int.Parse(ParsedSettings[5]);
                        int min = int.Parse(ParsedSettings[6]);
                        int sec = int.Parse(ParsedSettings[7]);
                        //If date is set to 00.00.00 then set it as a daily trigger
                        if (year == 0 && month == 0 && day == 0)
                        {
                            year = DateTime.Now.Year;
                            month = DateTime.Now.Month;
                            day = DateTime.Now.Day;
                            Daily = true;
                        }
                        //Sets the date/time to trigger on.
                        TriggerDate = new DateTime(year, month, day, hour, min, sec);
                    }
                    catch
                    {
                        //Warn that there is invalid setting
                        plugin.Puts("Invalid Date/Time Setting in clock @ " + plugin.getGrid(ThisCounter.transform.position));
                        return;
                    }
                    try
                    {
                        //Loads the delay in closing
                        CloseDelay = int.Parse(ParsedSettings[8]);
                    }
                    catch
                    {
                        //Warn that there is invalid setting
                        plugin.Puts("Invalid CloseDelay Setting in clock @ " + plugin.getGrid(ThisCounter.transform.position));
                        return;
                    }
                    //No warning on this if it fails since could just be left empty.
                    try
                    {
                        CustomText = ParsedSettings[9];
                    }
                    catch
                    { }
                }
                //Find what position the counter is
                switch (plugin.CheckMe(ThisCounter))
                {
                    case 0:
                        Hours = ThisCounter;
                        break;
                    case 1:
                        Mins = ThisCounter;
                        //Add the output at the middle of the clock
                        Output = plugin.AddOutput(Mins);
                        break;
                    case 2:
                        Sec = ThisCounter;
                        break;
                }
                //Setup updates
                InvokeRepeating("tick", 1, 1);
            }

            void CheckOutputs(DateTimeOffset localTime)
            {
                if (Daily)
                {
                    //Over-rides the year,month and day to make output daily.
                    localTime = new DateTime(TriggerDate.Year, TriggerDate.Month, TriggerDate.Day, localTime.Hour, localTime.Minute, localTime.Second);
                }

                //Check Trigger event
                if (localTime >= TriggerDate)
                {
                    //Switch on if not on.
                    if (!Output.HasFlag(BaseEntity.Flags.Reserved8) && !triggered)
                    {
                        triggered = true;
                        //toggle IO path
                        plugin.FollowIOPath(Output, true);
                        //If announcements enabled and a custom text is set make annoucement
                        if (plugin.Announcements && CustomText != "")
                        {
                            plugin.CreateAnouncment(CustomText, true, Output.transform.position, CloseDelay);
                        }
                        //Add disable timer if set
                        if (CloseDelay != 0)
                        {
                            Invoke("switchoff", CloseDelay);
                        }
                    }
                }
                else
                {
                    triggered = false;
                    //Switch off if on.
                    if (Output.HasFlag(BaseEntity.Flags.Reserved8))
                    {
                        //If announcements enabled and a custom text is set make annoucement
                        if (plugin.Announcements && CustomText != "")
                        {
                            plugin.CreateAnouncment(CustomText, false, Output.transform.position);
                        }
                        //toggle IO Path
                        plugin.FollowIOPath(Output, false);
                    }
                }
            }

            void switchoff()
            {
                //Send the announcement if its enabled and has a custom text
                if (plugin.Announcements && CustomText != "")
                {
                    plugin.CreateAnouncment(CustomText, false, Output.transform.position);
                }
                //Disable the IO
                plugin.FollowIOPath(Output, false);
            }

            void tick()
            {
                //Gets servers time
                DateTimeOffset localTime = Sky.Cycle.DateTime;
                if (RealTime)
                {
                    //Gets realtime
                    localTime = new DateTimeOffset(DateTime.Now);
                    localTime.AddDays(plugin.D);
                    localTime.AddHours(plugin.H);
                    localTime.AddMinutes(plugin.M);
                    localTime.AddSeconds(plugin.S);
                }
                //If clock is a output check its conditions
                if (Output != null)
                {
                    CheckOutputs(localTime);
                }

                //Do count down Function
                if (Countdown)
                {
                    if (Daily)
                    {
                        //Over-rides the year,month and day to make output daily.
                        localTime = new DateTime(TriggerDate.Year, TriggerDate.Month, TriggerDate.Day, localTime.Hour, localTime.Minute, localTime.Second);
                        localTime.AddDays(-1);
                    }
                    //Work out the difference between nows time and trigger time.
                    TimeSpan span = TriggerDate.Subtract(new DateTime(localTime.Ticks));
                    int Daysdiff = span.Days;
                    int Secondsdiff = span.Seconds;
                    int Minutesdiff = span.Minutes;
                    int Hoursdiff = span.Hours + (24 * Daysdiff);
                    if (localTime >= TriggerDate)
                    {
                        //Show Nothing since count down finished
                        Secondsdiff = 0;
                        Minutesdiff = 0;
                        Hoursdiff = 0;
                    }

                    if (Hours != null)
                    {
                        //Update hour counter if its changed
                        if (Hours.counterNumber != Hoursdiff)
                        {
                            Hours.SetCounterNumber(Hoursdiff);
                            Hours.SendNetworkUpdateImmediate();
                        }
                        return;
                    }
                    if (Mins != null)
                    {
                        //Update min counter if its changed
                        if (Mins.counterNumber != Minutesdiff)
                        {
                            Mins.SetCounterNumber(Minutesdiff);
                            Mins.SendNetworkUpdateImmediate();
                        }
                        return;
                    }
                    if (Sec != null)
                    {
                        //Update sec counter if its changed
                        if (Sec.counterNumber != Secondsdiff)
                        {
                            Sec.SetCounterNumber(Secondsdiff);
                            Sec.SendNetworkUpdateImmediate();
                        }
                        return;
                    }
                    return;
                }

                if (Hours != null)
                {
                    if (!hour24)
                    {
                        //12 hour clock layout
                        int h = int.Parse(localTime.ToString("hh"));
                        if (Hours.counterNumber != h)
                        {
                            //use little hh for 12 hour layout
                            Hours.SetCounterNumber(h);
                            Hours.SendNetworkUpdateImmediate();
                            return;
                        }
                    }
                    else
                    {
                        //24 hour clock layout
                        int h = int.Parse(localTime.ToString("HH"));
                        if (Hours.counterNumber != h)
                        {
                            Hours.SetCounterNumber(h);
                            Hours.SendNetworkUpdateImmediate();
                            return;
                        }
                    }
                }
                if (Mins != null)
                {
                    //min layout
                    int m = int.Parse(localTime.ToString("mm"));
                    if (Mins.counterNumber != m)
                    {
                        Mins.SetCounterNumber(m);
                        Mins.SendNetworkUpdateImmediate();
                        return;
                    }
                }
                if (Sec != null)
                {
                    //seconds layout
                    int s = int.Parse(localTime.ToString("ss"));
                    if (Sec.counterNumber != s)
                    {
                        Sec.SetCounterNumber(s);
                        Sec.SendNetworkUpdateImmediate();
                        return;
                    }
                }
            }
        }
        #endregion
    }
}
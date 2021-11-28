using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PrefabClock", "bmgjet", "1.0.0")]
    [Description("Controls bmgjets clock prefab")]
    public class PrefabClock : RustPlugin
    {
        #region Vars
        //Permission to see IO Output
        const string SeeIOOutput = "PrefabClock.See";

        //Settings

        //TimeZoneOffset
        public int H = 0;
        public int M = 0;

        //IO Output On Date Year,Month,Day,Hour,Min,Sec
        public bool EnableOutput = true;
        public DateTime TriggerDate = new DateTime(2021, 11, 26, 12,00,00);
        public bool Daily = true; //just uses time
        public int HowLong = 0; //Secs to keep output powered // 0 = until next day when daily or forever with date only.
        public int ResetDelay = 60; //Sec before allowing triggering again.
        public bool AnnounceDoors = true; //Announces in Chat when a Door has Opened
        public bool AnnounceOrSwitch = true; //Announces in Chat when A ORSwitch has been triggered by clock
        //End Settings

        public List<Vector3> HourFormat12 = new List<Vector3>();
        public List<Vector3> RealTime = new List<Vector3>();
        public bool ADCooldown;
        public bool AOSCooldown;
        private static PrefabClock plugin;
        #endregion

        #region Plugin Core
        private void Init()
        {
            permission.RegisterPermission(SeeIOOutput, this);
        }

        //Hides spawned ElectricalBlockers that are used as IO point
        object CanNetworkTo(ElectricalBlocker EB, BasePlayer player)
        {
            //ID it by owner id and skinid to return fast on other objects
            if (EB.OwnerID != 0 && EB.skinID != 264592) return null;
            //If player doesnt have permission to see disable them seeing it
            if (!player.IPlayer.HasPermission(SeeIOOutput))
            {
                return false;
            }
            return null;
        }

        private void OnServerInitialized(bool initial)
        {
            //Set up reference
            plugin = this;
            //Delay startup if fresh server boot. Helps hooking on slow servers
            if (initial)
            {
                Fstartup();
                return;
            }
            Startup();
        }

        private void Fstartup()
        {
            //Waits for player before running script to help first startup performance.
            timer.Once(10f, () =>
            {
                if (BasePlayer.activePlayerList.Count == 0)
                {
                    Fstartup();
                    return;
                }
                Startup();
            });
        }

        private void Startup()
        {
            int Clocks = 0;
            //Find All counters on the map
            for (int i = World.Serialization.world.prefabs.Count - 1; i >= 0; i--)
            {
                PrefabData prefabdata = World.Serialization.world.prefabs[i];
                //Check the prefab datas category since thats where customprefabs names are stored
                if (prefabdata.id == 4254177840 && prefabdata.category.Contains("BMGJETCLOCK"))
                {
                    //Scan found counters
                    PowerCounter PC = FindCounter(prefabdata.position, 0.1f);
                    if (PC == null || PC.GetComponent<PrefabClockAddon>() != null) return;
                    //Keep track of 12 hour clocks.
                    if (prefabdata.category.Contains("BMGJETCLOCK12"))
                    {
                        HourFormat12.Add(PC.transform.position);
                    }
                    //Keep track of clocks showing the real time instead of game time.
                    if (prefabdata.category.Contains("REALTIME"))
                    {
                        RealTime.Add(PC.transform.position);
                    }
                    //Power up counter so it displays numbers
                    PC.SetFlag(PowerCounter.Flag_HasPower, true);
                    //Disable showing power its passing
                    PC.SetFlag(PowerCounter.Flag_ShowPassthrough, false);
                    //Add clock script
                    PC.gameObject.AddComponent<PrefabClockAddon>();
                    PC.SendNetworkUpdateImmediate();
                    Clocks++;
                }
            }
            Puts("Running " + Clocks.ToString() + " clock scripts");
        }

        private void Unload()
        {
            plugin = null;
            //Remove scripts incase its a plugin restart and not server restart.
            foreach (var ClockScripts in GameObject.FindObjectsOfType<PowerCounter>())
            {
                foreach (var cs in ClockScripts.GetComponentsInChildren<PrefabClockAddon>())
                {
                    UnityEngine.Object.DestroyImmediate(cs);
                }
            }
        }

        PowerCounter FindCounter(Vector3 pos, float radius)
        {
            //Debug shows where its scanning for admins with see permission
            foreach (BasePlayer BP in BasePlayer.activePlayerList)
            {
                if (BP.IsAdmin && BP.IPlayer.HasPermission(SeeIOOutput)) BP.SendConsoleCommand("ddraw.sphere", 8f, Color.blue, pos, radius);
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
                if(BP.IsAdmin && BP.IPlayer.HasPermission(SeeIOOutput)) BP.SendConsoleCommand("ddraw.sphere", 8f, Color.green, pos, radius);
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

        //Logic behind toggling the IO
        IOEntity ToggleIO(IOEntity FollowIOPath, bool enabled)
        {
            //If its not already enabled add a timer to disable it
            if(!FollowIOPath.HasFlag(BaseEntity.Flags.Reserved8))
            {
                DisableTimer(FollowIOPath);
            }
            //Enable it
            FollowIOPath.SetFlag(BaseEntity.Flags.Reserved8, enabled);
            FollowIOPath.SetFlag(IOEntity.Flag_HasPower, enabled);
            FollowIOPath.SetFlag(BaseEntity.Flags.On, enabled);

            //Cast as Igniter to do some extra functions if its a Igniter
            Igniter igniter = FollowIOPath as Igniter;
            if (igniter != null)
            {
                igniter.SelfDamagePerIgnite = 0f;
                if (enabled)
                {
                    igniter.UpdateHasPower(100, 0);
                }
                else
                {
                    igniter.UpdateHasPower(0, 0);
                }
                return null;
            }

            //Cast as Doormanipulator to do some extra functions if its a Doormanipulator
            DoorManipulator DoorMan = FollowIOPath as DoorManipulator;
            if (DoorMan != null)
            {
                DoorMan.SetFlag(DoorManipulator.Flags.Open, enabled);
                //Triggers the action
                DoorMan.DoAction();
                if (AnnounceDoors && !ADCooldown)
                {
                    if (enabled)
                    {
                        ADCooldown = true;
                        //Cool down to stop chat spam with logs of doors.
                        timer.Once(5f, () =>
                         {
                             ADCooldown = false;
                         });
                        CreateAnouncment("Door @ " + plugin.getGrid(DoorMan.transform.position) + " <color=green>Opened</color>");
                    }
                    else
                    {
                        ADCooldown = true;
                        timer.Once(5f, () =>
                        {
                            ADCooldown = false;
                        });
                        CreateAnouncment("Door @ " + plugin.getGrid(DoorMan.transform.position) + " <color=red>Closed</color>");
                    }
                }
            }
            //Handle Or switch differently then following its path and enabling each item.
            ORSwitch OrSwitch = FollowIOPath as ORSwitch;
            if (OrSwitch != null)
            {
                if (enabled)
                {
                    if (AnnounceOrSwitch && !AOSCooldown)
                    {
                        AOSCooldown = true;
                        timer.Once(5f, () =>
                        {
                            AOSCooldown = false;
                        });
                        CreateAnouncment("Event: <color=green>Enabled</color> @ " + plugin.getGrid(OrSwitch.transform.position));
                    }
                        OrSwitch.UpdateFromInput(100, 0);
                }
                else
                {
                    if (AnnounceOrSwitch && !AOSCooldown)
                    {
                        AOSCooldown = true;
                        timer.Once(5f, () =>
                        {
                            AOSCooldown = false;
                        });
                        CreateAnouncment("Event: <color=red>Disabled</color> @ " + plugin.getGrid(OrSwitch.transform.position));
                    }
                    OrSwitch.UpdateFromInput(0, 0);
                }
                return null;
            }
            //Handle Teslacoil if using timer on one as a trap.
            TeslaCoil Tcoil = FollowIOPath as TeslaCoil;
            if (Tcoil != null)
            {
                Tcoil.SetFlag(TeslaCoil.Flag_StrongShorting, enabled);
                Tcoil.maxDischargeSelfDamageSeconds = 999999f;
                if (enabled)
                {
                    Tcoil.InvokeRepeating(Tcoil.Discharge, 1, 0.5f);
                }
                else
                {
                    Tcoil.CancelInvoke(Tcoil.Discharge);
                }
                return null;
            }
            //Send update to clients
            FollowIOPath.SendNetworkUpdateImmediate();
            //Return the next IO in the connection
            return FollowIOPath.outputs[0].connectedTo.ioEnt;
        }

        public void DisableTimer(IOEntity IOE)
        {
            //Only create a disable timer if a time to run is set.
            if (HowLong > 0)
            {
                timer.Once(HowLong, () =>
                 {
                     ToggleIO(IOE, false);
                 });
            }
        }

        //Sends message to all active players under a steamID
        void CreateAnouncment(string msg)
        {
            foreach (BasePlayer current in BasePlayer.activePlayerList.ToArray())
            {
                if (current.IsConnected)
                {
                    rust.SendChatMessage(current, "", msg, "76561197980831914");
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

        void DestroyGroundComp(BaseEntity ent)
        {
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<GroundWatch>());
            //Stops Decay
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<DeployableDecay>());
        }

        void DestroyMeshCollider(BaseEntity ent)
        {
            foreach (var mesh in ent.GetComponentsInChildren<MeshCollider>())
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
        #endregion

        #region Clock Scripts
        private class PrefabClockAddon : MonoBehaviour
        {
            //Use script on the 3 counters.
            private PowerCounter Hours;
            private PowerCounter Mins;
            private PowerCounter Sec;
            private ElectricalBlocker Output;
            //Get game time based on sky.
            TOD_Sky Sky = TOD_Sky.Instance;
            //Check if using the real time
            private bool RealTime = false;
            //Logic to stop spamming events
            private bool HasTrigged = false;
            private long CoolDown = plugin.HowLong + plugin.ResetDelay;

            private void Awake()
            {
                PowerCounter ThisCounter = GetComponent<PowerCounter>();
                //Check what time of clock to use
                if (plugin.RealTime.Contains(ThisCounter.transform.position))
                {
                    RealTime = true;
                }
                //Find what position the counter is
                switch (CheckMe(ThisCounter))
                {
                    case 0:
                        Hours = ThisCounter;
                        break;
                    case 1:
                        Mins = ThisCounter;
                        AddOutput();
                        break;
                    case 2:
                        Sec = ThisCounter;
                        break;
                }
                //Setup updates
                InvokeRepeating("tick", 1, 1);
            }

            //Create IO point on the middle counter
            void AddOutput()
            {
                //Adjust position
                Vector3 pos = Mins.transform.position;
                pos -= Mins.transform.forward * 0.058f;
                pos += Mins.transform.up * 0.168f;
                ElectricalBlocker EB = plugin.FindSocket(pos, 0.5f);
                //Check if outputs is enabled other wise destroy if ones has been created there.
                if (!plugin.EnableOutput)
                {
                    if (EB == null)
                    {
                        return;
                    }
                    EB.Kill();
                    return;
                }
                //If no blocker found create one.
                if (EB == null)
                {
                    ElectricalBlocker OutputSocket = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/gates/blocker/electrical.blocker.deployed.prefab", Mins.transform.position, Mins.transform.rotation) as ElectricalBlocker;
                    if (OutputSocket == null) return;
                    plugin.DestroyGroundComp(OutputSocket);
                    plugin.DestroyMeshCollider(OutputSocket);
                    OutputSocket.transform.position = pos;
                    //How its identified
                    OutputSocket.OwnerID = 0;
                    OutputSocket.skinID = 264592;
                    //create
                    OutputSocket.Spawn();
                    //Reference
                    Output = EB;
                }
                else
                {
                    //Link to a blocker if already present since must be server restart.
                    plugin.DestroyGroundComp(EB);
                    plugin.DestroyMeshCollider(EB);
                    Output = EB;
                }
            }

            void CheckOutputs(DateTimeOffset localTime)
            {
                if (plugin.Daily)
                {
                    //Over-rides the year,month and day to make output daily.
                    localTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, localTime.Hour, localTime.Minute, localTime.Second);
                }

                //Check Trigger event
                if (localTime >= plugin.TriggerDate)
                {
                    //Logic to stop spamming event switches
                    if (HasTrigged)
                    {
                        if (CoolDown <= 0)
                        {
                            HasTrigged = false;
                            CoolDown = plugin.HowLong + plugin.ResetDelay;
                        }
                        else
                        {
                            CoolDown--;
                            return;
                        }
                    }
                    //Switch on if not on.
                    if (!Output.HasFlag(BaseEntity.Flags.Reserved8))
                    {
                        HasTrigged = true;
                        //toggle IO path
                        var FollowIOPath = Output as IOEntity;
                        //Keep looping the path to trigger all things along it
                        while (FollowIOPath != null)
                        {
                            FollowIOPath = plugin.ToggleIO(FollowIOPath, true);
                        }
                    }
                }
                else
                {
                    //Switch off if on.
                    if (Output.HasFlag(BaseEntity.Flags.Reserved8))
                    {
                        //toggle IO Path
                        var FollowIOPath = Output as IOEntity;
                        //Keep looping the path to trigger all things along it
                        while (FollowIOPath != null)
                        {
                            FollowIOPath = plugin.ToggleIO(FollowIOPath, false);
                        }
                    }
                }
            }


            int CheckMe(BaseEntity be)
            {
                //Cast sphere either side to work out location of counter
                Vector3 Left = be.transform.position + be.transform.right * -0.2f;
                Vector3 Right = be.transform.position + be.transform.right * 0.2f;
                var CheckLeft = plugin.FindCounter(Left, 0.01f);
                var CheckRight = plugin.FindCounter(Right, 0.01f);
                if (CheckLeft && CheckRight) return 1;
                if (CheckLeft && !CheckRight) return 0;
                if (!CheckLeft && CheckRight) return 2;
                return -1;
            }

            void tick()
            {
                //Gets servers time
                DateTimeOffset localTime = Sky.Cycle.DateTime;
                if (RealTime)
                {
                    //Gets realtime
                    localTime = new DateTimeOffset(DateTime.Now);
                    localTime.AddHours(plugin.H);
                    localTime.AddMinutes(plugin.M);
                }
                //If clock is a out put check its conditions
                if(Output !=null)
                {
                    CheckOutputs(localTime);
                }
                if (Hours != null)
                {
                    if (plugin.HourFormat12.Contains(Hours.transform.position))
                    {
                        //12 hour clock layout
                        int h = int.Parse(localTime.ToString("hh"));
                        if (Hours.counterNumber != h)
                        {
                            Hours.SetCounterNumber(int.Parse(localTime.ToString("hh")));
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
                            Hours.SetCounterNumber(int.Parse(localTime.ToString("HH")));
                            Hours.SendNetworkUpdateImmediate();
                        }
                    }
                }
                if (Mins != null)
                {
                    //min layout
                    int m = int.Parse(localTime.ToString("mm"));
                    if (Mins.counterNumber != m)
                    {
                        Mins.SetCounterNumber(int.Parse(localTime.ToString("mm")));
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
                    }
                }
            }
        }
        #endregion
    }
}
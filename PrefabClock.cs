using ProtoBuf;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PrefabClock", "bmgjet", "1.0.0")]
    [Description("Controls bmgjets clock prefab")]
    public class PrefabClock : RustPlugin
    {
        #region Vars
        //TimeZoneOffset
        public int H = 0;
        public int M = 0;

        public List<Vector3> HourFormat12 = new List<Vector3>();
        public List<Vector3> RealTime = new List<Vector3>();
        private static PrefabClock plugin;
        #endregion

        #region Plugin Core
        private void OnServerInitialized(bool initial)
        {
            plugin = this;
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
            //Find All counters
            for (int i = World.Serialization.world.prefabs.Count - 1; i >= 0; i--)
            {
                PrefabData prefabdata = World.Serialization.world.prefabs[i];
                if (prefabdata.id == 4254177840 && prefabdata.category.Contains("BMGJETCLOCK"))
                {
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
                    PC.SetFlag(PowerCounter.Flag_HasPower, true);
                    PC.SetFlag(PowerCounter.Flag_ShowPassthrough, false);
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
            List<PowerCounter> Counters = new List<PowerCounter>();
            Vis.Entities<PowerCounter>(pos, radius, Counters);
            foreach (PowerCounter PC in Counters)
            {
                    return PC;
            }
            return null;
        }
        #endregion

        #region Scripts
        private class PrefabClockAddon : MonoBehaviour
        {
            private PowerCounter Hours;
            private PowerCounter Mins;
            private PowerCounter Sec;
            TOD_Sky Sky = TOD_Sky.Instance;
            private bool RealTime = false;

            private void Awake()
            {
                PowerCounter ThisCounter = GetComponent<PowerCounter>();
                if (plugin.RealTime.Contains(ThisCounter.transform.position))
                {
                    RealTime = true;
                }

                switch (CheckMe(ThisCounter))
                {
                    case 0:
                        Hours = ThisCounter;
                        break;
                    case 1:
                        Mins = ThisCounter;
                        break;
                    case 2:
                        Sec = ThisCounter;
                        break;
                }
                //Setup updates
                InvokeRepeating("tick", 1, 1);
            }

            int CheckMe(BaseEntity be)
            {
                //Cast sphere either side to work out location
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
                if (Hours != null)
                {
                    //12 or 24 hour switch
                    if (plugin.HourFormat12.Contains(Hours.transform.position))
                    {
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
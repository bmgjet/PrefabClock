<h1><strong>Information Out of Date, will update tomorrow or look inside plugin code for simple explination<br /></strong></h1>
<br><br>
<h1><strong>PrefabClock<br /></strong></h1>
<p><img src="https://github.com/bmgjet/PrefabClock/raw/main/PrefabClock.png" alt="" /></p>
<p>Install Plugin in Oxide/Plugins folder.<br />Edit plugin files settings to match those you require<br />Place Prefabs in RustEdits Custom Prefabs folder.<br />Use prefabs on map,</p>
<ul>
<li>BMGJETCLOCK12 = Game time shown in 12 hour.</li>
<li>BMGJETCLOCK24 = Game time shown in 24 hour.</li>
<li>BMGJETCLOCK12REALTIME = Servers time shown in 12 hour.</li>
<li>BMGJETCLOCK24REALTIME = Servers time shown in 24 hour.</li>
</ul>
<p><br />A offset has been provided within the the plugin to adjust time if your servers not hosted in your timezone.<br /><br />IO can be added to the clock by placing a Electrical Blocker Deployed on the back of the Clock Prefab.<br /><br />IO can also be edited from within a live server.<br />To enable viewing of the IO use chat command<br /><strong>/clockview true</strong></p>
<p>Using the command<br /><strong>/clockview false</strong><br />Will return the IO to being Invisable.<br /><br />If for some reason IO gets stuck you and reload it with<br /><strong>/clockreload</strong><br /><br />To Fully reset clock IO you can use command<br /><strong>/clockreset<br /><br /><br />Settings within the plugin:<br /></strong></p>
<p><span style="color: #008000;">//TimeZoneOffset<br /></span><span style="color: #333399;">This is where you can add or subtract time the time from the servers time if your in a different time zone.</span><br />public int H = 0; <span style="color: #008000;">//Hours</span><br />public int M = 0;<span style="color: #008000;"> //Mins</span></p>
<p><span style="color: #008000;">//IO Output On Date Year,Month,Day,Hour,Min,Sec<br /></span>public bool EnableOutput = true; <span style="color: #008000;">//Disable IO only show time</span><br />public DateTime TriggerDate = new DateTime(2021, 11, 26, 12,00,00);<br />public bool Daily = true; <span style="color: #008000;">//just uses time and ignores year,month,day</span><br />public int HowLong = 0; <span style="color: #008000;">//Secs to keep output powered 0 = no auto shut off.<br /><span style="color: #000080;">If 0 has been passed then it wil stay enabled until trigger conditions fail.</span></span><br />public int ResetDelay = 5; <span style="color: #008000;">//Sec before allowing triggering again.<br /><span style="color: #000080;">Small delay that helps with multi firing of activations.</span><br /></span>public bool AnnounceDoors = true; <span style="color: #008000;">//Announces in Chat when a Door has Opened</span><br />public bool AnnounceOrSwitch = true;<span style="color: #008000;"> //Announces in Chat when A ORSwitch has been triggered by clock</span><br /><span style="color: #008000;">//End Settings</span></p>
<p><br /><strong>A short useage Video can be seen here:</strong><br /><a href="https://www.youtube.com/watch?v=78yiPi37Ikc">https://www.youtube.com/watch?v=78yiPi37Ikc</a></p>

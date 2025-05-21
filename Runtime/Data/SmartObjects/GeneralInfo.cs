
namespace Balancy.Data.SmartObjects
{
    public class GeneralInfo : Balancy.Data.BaseData 
    {
        
		private int _playTime;
		private string _appVersion;
		private string _engineVersion;
		private string _platform;
		private int _platformId;
		private int _balancyPlatformId;
		private string _systemLanguage;
		private string _country;
		private int _timeSincePurchase;
		private int _session;
		private bool _isNewUser;
		private int _firstLoginTime;
		private int _level;
		private int _tutorialStep;
		private string _trafficSource;
		private bool _dontDisturb;
		private int _lastLoginTime;
		private int _currentDay;
		private string _trafficCampaign;
		private string _profileId;
		private string _deviceId;
		private string _customId;
		private string _gameLocalization;
		private int _timeSinceInstall;
		private string _deviceModel;
		private string _deviceName;
		private int _deviceType;
		private string _operatingSystem;
		private int _operatingSystemFamily;
		private int _systemMemorySize;
		private string _installVersion;
        
        
		public int PlayTime
		{
			get => _playTime;
			// set => SetIntValue("playTime", value);
		}
		public string AppVersion
		{
			get => _appVersion;
			// set => SetStringValue("appVersion", value);
		}
		public string EngineVersion
		{
			get => _engineVersion;
			// set => SetStringValue("engineVersion", value);
		}
		public string Platform
		{
			get => _platform;
			// set => SetStringValue("platform", value);
		}
		public int PlatformId
		{
			get => _platformId;
			// set => SetStringValue("platform", value);
		}
		
		public int BalancyPlatformId
		{
			get => _balancyPlatformId;
			// set => SetStringValue("balancyPlatformId", value);
		}
		
		public string SystemLanguage
		{
			get => _systemLanguage;
			// set => SetStringValue("systemLanguage", value);
		}
		public string Country
		{
			get => _country;
			// set => SetStringValue("country", value);
		}
		public int TimeSincePurchase
		{
			get => _timeSincePurchase;
			// set => SetIntValue("timeSincePurchase", value);
		}
		public int Session
		{
			get => _session;
			// set => SetIntValue("session", value);
		}
		public bool IsNewUser
		{
			get => _isNewUser;
			// set => SetBoolValue("isNewUser", value);
		}
		public int FirstLoginTime
		{
			get => _firstLoginTime;
			// set => SetIntValue("firstLoginTime", value);
		}
		public int Level
		{
			get => _level;
			set => SetIntValue("level", value);
		}
		public int TutorialStep
		{
			get => _tutorialStep;
			set => SetIntValue("tutorialStep", value);
		}
		public string TrafficSource
		{
			get => _trafficSource;
			set => SetStringValue("trafficSource", value);
		}
		public bool DontDisturb
		{
			get => _dontDisturb;
			set => SetBoolValue("dontDisturb", value);
		}
		public int LastLoginTime
		{
			get => _lastLoginTime;
			// set => SetIntValue("lastLoginTime", value);
		}
		public int CurrentDay
		{
			get => _currentDay;
			// set => SetIntValue("currentDay", value);
		}
		public string TrafficCampaign
		{
			get => _trafficCampaign;
			set => SetStringValue("trafficCampaign", value);
		}
		public string ProfileId
		{
			get => _profileId;
			// set => SetStringValue("profileId", value);
		}
		public string DeviceId
		{
			get => _deviceId;
			// set => SetStringValue("deviceId", value);
		}
		public string CustomId
		{
			get => _customId;
			set => SetStringValue("customId", value);
		}
		public string GameLocalization
		{
			get => _gameLocalization;
			// set => SetStringValue("gameLocalization", value);
		}
		public int TimeSinceInstall
		{
			get => _timeSinceInstall;
			// set => SetIntValue("timeSinceInstall", value);
		}
		public string DeviceModel
		{
			get => _deviceModel;
			// set => SetStringValue("deviceModel", value);
		}
		public string DeviceName
		{
			get => _deviceName;
			// set => SetStringValue("deviceName", value);
		}
		public int DeviceType
		{
			get => _deviceType;
			// set => SetIntValue("deviceType", value);
		}
		public string OperatingSystem
		{
			get => _operatingSystem;
			// set => SetStringValue("operatingSystem", value);
		}
		public int OperatingSystemFamily
		{
			get => _operatingSystemFamily;
			// set => SetIntValue("operatingSystemFamily", value);
		}
		public int SystemMemorySize
		{
			get => _systemMemorySize;
			// set => SetIntValue("systemMemorySize", value);
		}
		public string InstallVersion
		{
			get => _installVersion;
			// set => SetStringValue("installVersion", value);
		}
        
        public override void InitData()
        {
            base.InitData();
            
			InitAndSubscribeForParamChange("playTime", Update_playTime);
			InitAndSubscribeForParamChange("appVersion", Update_appVersion);
			InitAndSubscribeForParamChange("engineVersion", Update_engineVersion);
			InitAndSubscribeForParamChange("platform", Update_platform);
			InitAndSubscribeForParamChange("platformId", Update_platformId);
			InitAndSubscribeForParamChange("balancyPlatformId", Update_balancyPlatformId);
			InitAndSubscribeForParamChange("systemLanguage", Update_systemLanguage);
			InitAndSubscribeForParamChange("country", Update_country);
			InitAndSubscribeForParamChange("timeSincePurchase", Update_timeSincePurchase);
			InitAndSubscribeForParamChange("session", Update_session);
			InitAndSubscribeForParamChange("isNewUser", Update_isNewUser);
			InitAndSubscribeForParamChange("firstLoginTime", Update_firstLoginTime);
			InitAndSubscribeForParamChange("level", Update_level);
			InitAndSubscribeForParamChange("tutorialStep", Update_tutorialStep);
			InitAndSubscribeForParamChange("trafficSource", Update_trafficSource);
			InitAndSubscribeForParamChange("dontDisturb", Update_dontDisturb);
			InitAndSubscribeForParamChange("lastLoginTime", Update_lastLoginTime);
			InitAndSubscribeForParamChange("currentDay", Update_currentDay);
			InitAndSubscribeForParamChange("trafficCampaign", Update_trafficCampaign);
			InitAndSubscribeForParamChange("profileId", Update_profileId);
			InitAndSubscribeForParamChange("deviceId", Update_deviceId);
			InitAndSubscribeForParamChange("customId", Update_customId);
			InitAndSubscribeForParamChange("gameLocalization", Update_gameLocalization);
			InitAndSubscribeForParamChange("timeSinceInstall", Update_timeSinceInstall);
			InitAndSubscribeForParamChange("deviceModel", Update_deviceModel);
			InitAndSubscribeForParamChange("deviceName", Update_deviceName);
			InitAndSubscribeForParamChange("deviceType", Update_deviceType);
			InitAndSubscribeForParamChange("operatingSystem", Update_operatingSystem);
			InitAndSubscribeForParamChange("operatingSystemFamily", Update_operatingSystemFamily);
			InitAndSubscribeForParamChange("systemMemorySize", Update_systemMemorySize);
			InitAndSubscribeForParamChange("installVersion", Update_installVersion);
        }
        
		private void Update_playTime() { _playTime = GetIntParam("playTime"); }
		private void Update_appVersion() { _appVersion = GetStringParam("appVersion"); }
		private void Update_engineVersion() { _engineVersion = GetStringParam("engineVersion"); }
		private void Update_platform() { _platform = GetStringParam("platform"); }
		private void Update_platformId() { _platformId = GetIntParam("platformId"); }
		private void Update_balancyPlatformId() { _balancyPlatformId = GetIntParam("balancyPlatformId"); }
		private void Update_systemLanguage() { _systemLanguage = GetStringParam("systemLanguage"); }
		private void Update_country() { _country = GetStringParam("country"); }
		private void Update_timeSincePurchase() { _timeSincePurchase = GetIntParam("timeSincePurchase"); }
		private void Update_session() { _session = GetIntParam("session"); }
		private void Update_isNewUser() { _isNewUser = GetBoolParam("isNewUser"); }
		private void Update_firstLoginTime() { _firstLoginTime = GetIntParam("firstLoginTime"); }
		private void Update_level() { _level = GetIntParam("level"); }
		private void Update_tutorialStep() { _tutorialStep = GetIntParam("tutorialStep"); }
		private void Update_trafficSource() { _trafficSource = GetStringParam("trafficSource"); }
		private void Update_dontDisturb() { _dontDisturb = GetBoolParam("dontDisturb"); }
		private void Update_lastLoginTime() { _lastLoginTime = GetIntParam("lastLoginTime"); }
		private void Update_currentDay() { _currentDay = GetIntParam("currentDay"); }
		private void Update_trafficCampaign() { _trafficCampaign = GetStringParam("trafficCampaign"); }
		private void Update_profileId() { _profileId = GetStringParam("profileId"); }
		private void Update_deviceId() { _deviceId = GetStringParam("deviceId"); }
		private void Update_customId() { _customId = GetStringParam("customId"); }
		private void Update_gameLocalization() { _gameLocalization = GetStringParam("gameLocalization"); }
		private void Update_timeSinceInstall() { _timeSinceInstall = GetIntParam("timeSinceInstall"); }
		private void Update_deviceModel() { _deviceModel = GetStringParam("deviceModel"); }
		private void Update_deviceName() { _deviceName = GetStringParam("deviceName"); }
		private void Update_deviceType() { _deviceType = GetIntParam("deviceType"); }
		private void Update_operatingSystem() { _operatingSystem = GetStringParam("operatingSystem"); }
		private void Update_operatingSystemFamily() { _operatingSystemFamily = GetIntParam("operatingSystemFamily"); }
		private void Update_systemMemorySize() { _systemMemorySize = GetIntParam("systemMemorySize"); }
		private void Update_installVersion() { _installVersion = GetStringParam("installVersion"); }
    }
}

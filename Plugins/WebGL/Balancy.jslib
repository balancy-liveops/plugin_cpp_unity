mergeInto(LibraryManager.library, {
  balancySetLogCallback: function (callbackPtr) {
       console.log("SET balancySetLogCallback 1, ptr =", callbackPtr);
       Module.BalancyBridge.module._balancySetLogCallback(callbackPtr);
    },

balancyLoadAndInit: function (onReadyCallback) {
    if (!Module.BalancyBridge) {
      Module.BalancyBridge = {
        module: null,
        initialized: false,
        onReadyCallback: 0
      };
    }

    var bridge = Module.BalancyBridge;

    if (bridge.initialized) {
      console.log("⚠️ Balancy already initialized");
      dynCall('v', onReadyCallback);
      return;
    }

    if (bridge.module) {
      console.log("⏳ Balancy is loading...");
      bridge.onReadyCallback = onReadyCallback;
      return;
    }

    console.log("📦 Loading Balancy.js...");

    var script = document.createElement('script');
    script.src = 'StreamingAssets/Balancy.js';

    script.onload = function () {
      console.log("✅ Balancy.js loaded");

      var instance = Balancy({
        locateFile: function (path) {
          if (path.endsWith('.wasm')) return 'StreamingAssets/Balancy.wasm';
          return path;
        },
        onRuntimeInitialized: function () {
          console.log("🚀 Balancy initialized");
          bridge.initialized = true;
          bridge.module = this;

          if (bridge.onReadyCallback) {
            dynCall('v', bridge.onReadyCallback);
            bridge.onReadyCallback = 0;
          }
        }
      });
    };

    script.onerror = function () {
      console.error("❌ Failed to load Balancy.js");
    };

    bridge.onReadyCallback = onReadyCallback;
    document.body.appendChild(script);
  },

  balancySetInvokeInMainThreadCallback: function() { return Module.BalancyBridge.module._balancySetInvokeInMainThreadCallback.apply(null, arguments); },
  balancyInvokeMethodInMainThread: function() { return Module.BalancyBridge.module._balancyInvokeMethodInMainThread.apply(null, arguments); },
  balancyInitUnityFileHelper: function(persistentDataPath, assetDataPath, loadFromResources, isFileExistsCallback) { return Module.BalancyBridge.module._balancyInitUnityFileHelper(persistentDataPath, assetDataPath, loadFromResources, isFileExistsCallback); },
  balancyInit: function(appConfig) { return Module.BalancyBridge.module._balancyInit(appConfig); },
  balancyStop: function() { return Module.BalancyBridge.module._balancyStop.apply(null, arguments); },
  balancyGetModelByUnnyId: function() { return Module.BalancyBridge.module._balancyGetModelByUnnyId.apply(null, arguments); },
  balancyGetModels: function() { return Module.BalancyBridge.module._balancyGetModels.apply(null, arguments); },
  balancyGetObjectParam: function() { return Module.BalancyBridge.module._balancyGetObjectParam.apply(null, arguments); },
  balancyGetObjectArrayParam: function() { return Module.BalancyBridge.module._balancyGetObjectArrayParam.apply(null, arguments); },
  balancyGetIntParam: function() { return Module.BalancyBridge.module._balancyGetIntParam.apply(null, arguments); },
  balancyGetLongParam: function() { return Module.BalancyBridge.module._balancyGetLongParam.apply(null, arguments); },
  balancyGetBoolParam: function() { return Module.BalancyBridge.module._balancyGetBoolParam.apply(null, arguments); },
  balancyGetFloatParam: function() { return Module.BalancyBridge.module._balancyGetFloatParam.apply(null, arguments); },
  balancyGetIntArrayParam: function() { return Module.BalancyBridge.module._balancyGetIntArrayParam.apply(null, arguments); },
  balancyGetLongArrayParam: function() { return Module.BalancyBridge.module._balancyGetLongArrayParam.apply(null, arguments); },
  balancyGetFloatArrayParam: function() { return Module.BalancyBridge.module._balancyGetFloatArrayParam.apply(null, arguments); },
  balancyGetBoolArrayParam: function() { return Module.BalancyBridge.module._balancyGetBoolArrayParam.apply(null, arguments); },
  balancyFreeStringArray: function() { return Module.BalancyBridge.module._balancyFreeStringArray.apply(null, arguments); },
  balancySetModelOnRefresh: function() { return Module.BalancyBridge.module._balancySetModelOnRefresh.apply(null, arguments); },
  balancySetUserDataInitializedCallback: function() { return Module.BalancyBridge.module._balancySetUserDataInitializedCallback.apply(null, arguments); },
  balancySetProfileOnReset: function() { return Module.BalancyBridge.module._balancySetProfileOnReset.apply(null, arguments); },
  balancySetBaseDataParamChanged: function() { return Module.BalancyBridge.module._balancySetBaseDataParamChanged.apply(null, arguments); },
  balancySetBaseDataDestroyed: function() { return Module.BalancyBridge.module._balancySetBaseDataDestroyed.apply(null, arguments); },
  balancyConfigLaunch: function() { return Module.BalancyBridge.module._balancyConfigLaunch.apply(null, arguments); },
  balancyConfigClose: function() { return Module.BalancyBridge.module._balancyConfigClose.apply(null, arguments); },
  balancyConfigGetStatus: function() { return Module.BalancyBridge.module._balancyConfigGetStatus.apply(null, arguments); },
  balancyConfigLoadListOfGames: function() { return Module.BalancyBridge.module._balancyConfigLoadListOfGames.apply(null, arguments); },
  balancyConfigLoadBranches: function() { return Module.BalancyBridge.module._balancyConfigLoadBranches.apply(null, arguments); },
  balancyConfigAuth: function() { return Module.BalancyBridge.module._balancyConfigAuth.apply(null, arguments); },
  balancyConfigSignOut: function() { return Module.BalancyBridge.module._balancyConfigSignOut.apply(null, arguments); },
  balancyConfigGetSelectedBranchId: function() { return Module.BalancyBridge.module._balancyConfigGetSelectedBranchId.apply(null, arguments); },
  balancyConfigSetSelectedGame: function() { return Module.BalancyBridge.module._balancyConfigSetSelectedGame.apply(null, arguments); },
  balancyConfigSetSelectedBranch: function() { return Module.BalancyBridge.module._balancyConfigSetSelectedBranch.apply(null, arguments); },
  balancyConfigDownloadContentToResources: function() { return Module.BalancyBridge.module._balancyConfigDownloadContentToResources.apply(null, arguments); },
  balancyConfigGenerateCode: function() { return Module.BalancyBridge.module._balancyConfigGenerateCode.apply(null, arguments); },
  balancyGetProfile: function() { return Module.BalancyBridge.module._balancyGetProfile.apply(null, arguments); },
  balancyResetAllProfiles: function() { return Module.BalancyBridge.module._balancyResetAllProfiles.apply(null, arguments); },
  balancyGetBaseDataParam: function() { return Module.BalancyBridge.module._balancyGetBaseDataParam.apply(null, arguments); },
  balancyGetListBaseDataParam: function() { return Module.BalancyBridge.module._balancyGetListBaseDataParam.apply(null, arguments); },
  balancySubscribeBaseDataParamChange: function() { return Module.BalancyBridge.module._balancySubscribeBaseDataParamChange.apply(null, arguments); },
  balancyUnsubscribeBaseDataParamChange: function() { return Module.BalancyBridge.module._balancyUnsubscribeBaseDataParamChange.apply(null, arguments); },
  balancySmartListAddElement: function() { return Module.BalancyBridge.module._balancySmartListAddElement.apply(null, arguments); },
  balancySmartListGetSize: function() { return Module.BalancyBridge.module._balancySmartListGetSize.apply(null, arguments); },
  balancySmartListGetElementAt: function() { return Module.BalancyBridge.module._balancySmartListGetElementAt.apply(null, arguments); },
  balancySmartListRemoveElementAt: function() { return Module.BalancyBridge.module._balancySmartListRemoveElementAt.apply(null, arguments); },
  balancySmartListClear: function() { return Module.BalancyBridge.module._balancySmartListClear.apply(null, arguments); },
  balancySetIntParam: function() { return Module.BalancyBridge.module._balancySetIntParam.apply(null, arguments); },
  balancySetLongParam: function() { return Module.BalancyBridge.module._balancySetLongParam.apply(null, arguments); },
  balancySetStringParam: function() { return Module.BalancyBridge.module._balancySetStringParam.apply(null, arguments); },
  balancySetFloatParam: function() { return Module.BalancyBridge.module._balancySetFloatParam.apply(null, arguments); },
  balancySetBoolParam: function() { return Module.BalancyBridge.module._balancySetBoolParam.apply(null, arguments); },
  balancySystemProfileTrackRevenue: function() { return Module.BalancyBridge.module._balancySystemProfileTrackRevenue.apply(null, arguments); },
  balancyDataObjectLoad: function() { return Module.BalancyBridge.module._balancyDataObjectLoad.apply(null, arguments); },
  balancyDataObjectDeleteFromDisk: function() { return Module.BalancyBridge.module._balancyDataObjectDeleteFromDisk.apply(null, arguments); },
  balancySetTestMode: function() { return Module.BalancyBridge.module._balancySetTestMode.apply(null, arguments); },
  balancyLocalization_ChangeLocalization: function() { return Module.BalancyBridge.module._balancyLocalization_ChangeLocalization.apply(null, arguments); },
  balancyOfferInfo_GetSecondsLeftBeforeDeactivation: function() { return Module.BalancyBridge.module._balancyOfferInfo_GetSecondsLeftBeforeDeactivation.apply(null, arguments); },
  balancyOfferGroupInfo_CanPurchase: function() { return Module.BalancyBridge.module._balancyOfferGroupInfo_CanPurchase.apply(null, arguments); },
  balancyEventInfo_GetSecondsLeftBeforeDeactivation: function() { return Module.BalancyBridge.module._balancyEventInfo_GetSecondsLeftBeforeDeactivation.apply(null, arguments); },
  balancyGameEvent_GetSecondsLeftBeforeDeactivation: function() { return Module.BalancyBridge.module._balancyGameEvent_GetSecondsLeftBeforeDeactivation.apply(null, arguments); },
  balancyGameEvent_GetSecondsBeforeActivation: function() { return Module.BalancyBridge.module._balancyGameEvent_GetSecondsBeforeActivation.apply(null, arguments); },
  balancyProfile_GetLastCloudSyncTime: function() { return Module.BalancyBridge.module._balancyProfile_GetLastCloudSyncTime.apply(null, arguments); },
  balancySetTimeOffset: function() { return Module.BalancyBridge.module._balancySetTimeOffset.apply(null, arguments); },
  balancyGetTimeOffset: function() { return Module.BalancyBridge.module._balancyGetTimeOffset.apply(null, arguments); },
  balancyGetStatus: function() { return Module.BalancyBridge.module._balancyGetStatus.apply(null, arguments); },
  balancyDailyBonus_claimNextReward: function() { return Module.BalancyBridge.module._balancyDailyBonus_claimNextReward.apply(null, arguments); },
  balancyDailyBonus_canClaimNextReward: function() { return Module.BalancyBridge.module._balancyDailyBonus_canClaimNextReward.apply(null, arguments); },
  balancyDailyBonus_getSecondsTillTheNextReward: function() { return Module.BalancyBridge.module._balancyDailyBonus_getSecondsTillTheNextReward.apply(null, arguments); },
  balancySoftPurchaseStoreItem: function() { return Module.BalancyBridge.module._balancySoftPurchaseStoreItem.apply(null, arguments); },
  balancySoftPurchaseGameOffer: function() { return Module.BalancyBridge.module._balancySoftPurchaseGameOffer.apply(null, arguments); },
  balancySoftPurchaseGameOfferGroup: function() { return Module.BalancyBridge.module._balancySoftPurchaseGameOfferGroup.apply(null, arguments); },
  balancyHardPurchaseStoreItem: function() { return Module.BalancyBridge.module._balancyHardPurchaseStoreItem.apply(null, arguments); },
  balancyHardPurchaseGameOffer: function() { return Module.BalancyBridge.module._balancyHardPurchaseGameOffer.apply(null, arguments); },
  balancyHardPurchaseGameOfferGroup: function() { return Module.BalancyBridge.module._balancyHardPurchaseGameOfferGroup.apply(null, arguments); },
  balancyHandleWebRequestComplete: function() { return Module.BalancyBridge.module._balancyHandleWebRequestComplete.apply(null, arguments); },
  balancyHandleFileLoadComplete: function() { return Module.BalancyBridge.module._balancyHandleFileLoadComplete.apply(null, arguments); },
  balancyRegisterWebRequestCallback: function() { return Module.BalancyBridge.module._balancyRegisterWebRequestCallback.apply(null, arguments); },
  balancyRegisterFileLoadCallback: function() { return Module.BalancyBridge.module._balancyRegisterFileLoadCallback.apply(null, arguments); },

  balancyGetInheritance: function() { return Module.BalancyBridge.module._balancyGetInheritance.apply(null, arguments); },
  balancyGetModelUnnyIds: function() { return Module.BalancyBridge.module._balancyGetModelUnnyIds.apply(null, arguments); },
  balancyGetStringArrayParam: function() { return Module.BalancyBridge.module._balancyGetStringArrayParam.apply(null, arguments); },
  balancyGetStringParam: function() { return Module.BalancyBridge.module._balancyGetStringParam.apply(null, arguments); },
  balancyGetTemplateName: function() { return Module.BalancyBridge.module._balancyGetTemplateName.apply(null, arguments); },
  balancyLocalization_GetAllLocalizationCodes: function() { return Module.BalancyBridge.module._balancyLocalization_GetAllLocalizationCodes.apply(null, arguments); },
  balancyLocalization_GetCurrentLocalizationCode: function() { return Module.BalancyBridge.module._balancyLocalization_GetCurrentLocalizationCode.apply(null, arguments); },
  balancyLocalization_GetLocalizedValue: function() { return Module.BalancyBridge.module._balancyLocalization_GetLocalizedValue.apply(null, arguments); }
});
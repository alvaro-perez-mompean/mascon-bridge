using System.Globalization;
using System.Resources;

namespace MasconBridge;

/// <summary>
/// Every piece of text the program shows. Generated from one table, so a key
/// cannot exist in one language and be missing from the other.
///
/// Which language is used comes from CultureInfo.CurrentUICulture, set at
/// startup from the configuration.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Rm =
        new("MasconBridge.Strings", typeof(Strings).Assembly);

    private static string Get(string key) =>
        Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    /// <summary>Every key, for tests that check both languages are complete.</summary>
    public static readonly string[] Keys =
    {
        "GroupDeviceAndAxis",
        "GroupCalibration",
        "GroupVirtualDevice",
        "GroupCurrentNotch",
        "LabelDevice",
        "LabelAxis",
        "LabelMinimum",
        "LabelMaximum",
        "LabelHysteresis",
        "LabelModel",
        "LabelLanguage",
        "ButtonRefresh",
        "ButtonCalibrate",
        "ButtonCalibrateFinish",
        "ButtonStartBridge",
        "ButtonStopBridge",
        "ButtonSaveConfiguration",
        "CheckInvertAxis",
        "CheckOverlay",
        "CheckPowerRelease",
        "CheckEmergencyRelease",
        "ButtonLearnRelease",
        "ButtonOverlayPlace",
        "ButtonOverlayPlaceDone",
        "ButtonLearnCancel",
        "HintAxis",
        "HintModel",
        "LabelDeviceName",
        "HintDeviceName",
        "HintButtonsInFile",
        "GroupCatches",
        "GroupOverlay",
        "HintOverlay",
        "HintCatches",
        "HintReleaseNotSet",
        "HintReleaseBinding",
        "HintReleasePress",
        "ButtonEditBindings",
        "DialogBindingsTitle",
        "DialogBindingsHint",
        "LabelHat",
        "HintHatBinding",
        "HintHatPress",
        "HintBindingNone",
        "ButtonClearBinding",
        "ButtonDialogOk",
        "ButtonDialogCancel",
        "HintEmergencyButton",
        "LabelGame",
        "GameNone",
        "GameJrEast",
        "HintGameDefaults",
        "FunctionAtsConfirmation",
        "FunctionHornLevel1",
        "FunctionHornLevel2",
        "FunctionDeadmanReset",
        "FunctionAlarmStop",
        "FunctionSpeedSuppression1",
        "FunctionCruiseControl",
        "FunctionSwitchCabinView",
        "FunctionPauseGame",
        "FunctionScreenshot",
        "FunctionDoNotUse",
        "FunctionNotInManual",
        "FunctionHat",
        "LabelVersion",
        "LinkUpdateAvailable",
        "CheckForUpdates",
        "HintLanguageRestart",
        "StatusStopped",
        "StatusBridgeRunning",
        "StatusCalibrating",
        "StatusNoMovement",
        "StatusConfigurationSaved",
        "StatusFailedToStart",
        "StatusNoJoystick",
        "StatusDevicesMoved",
        "StatusDeviceMissing",
        "DeviceItem",
        "NotchSending",
        "NotchPreview",
        "NotchHeldAtNeutral",
        "NotchHeldAtFullService",
        "NotchAxisMissing",
        "DialogCalibrationTitle",
        "DialogCalibrationNoMovement",
        "DialogStartTitle",
        "DialogStartFailed",
        "DialogSaveTitle",
        "DialogSaveFailed",
        "ConsoleUsage",
        "ConsoleUsageNoArgs",
        "ConsoleListHeader",
        "ConsoleNoJoystick",
        "ConsoleJoystickLine",
        "ConsoleAxisLine",
        "ConsoleButtonsLine",
        "ConsolePovLine",
        "ConsoleCalibrating",
        "ConsoleCalibrateHint",
        "ConsoleCalibrateLive",
        "ConsoleCalibrateNoMovement",
        "ConsoleCalibrateSaved",
        "ConsoleCreatingDevice",
        "ConsoleTestReady",
        "ConsoleTestCycling",
        "ConsoleTestNotch",
        "ConsoleRunHandle",
        "ConsoleRunInverted",
        "ConsoleRunZones",
        "ConsoleRunVirtual",
        "ConsoleRunLine",
        "ConsoleStopped",
        "ConsoleDeviceMoved",
        "ConsoleDeviceMissing",
        "ConfigCreatedSample",
        "ConfigUnreadable",
    };

    public static string GroupDeviceAndAxis => Get(nameof(GroupDeviceAndAxis));
    public static string GroupCalibration => Get(nameof(GroupCalibration));
    public static string GroupVirtualDevice => Get(nameof(GroupVirtualDevice));
    public static string GroupCurrentNotch => Get(nameof(GroupCurrentNotch));
    public static string LabelDevice => Get(nameof(LabelDevice));
    public static string LabelAxis => Get(nameof(LabelAxis));
    public static string LabelMinimum => Get(nameof(LabelMinimum));
    public static string LabelMaximum => Get(nameof(LabelMaximum));
    public static string LabelHysteresis => Get(nameof(LabelHysteresis));
    public static string LabelModel => Get(nameof(LabelModel));
    public static string LabelLanguage => Get(nameof(LabelLanguage));
    public static string ButtonRefresh => Get(nameof(ButtonRefresh));
    public static string ButtonCalibrate => Get(nameof(ButtonCalibrate));
    public static string ButtonCalibrateFinish => Get(nameof(ButtonCalibrateFinish));
    public static string ButtonStartBridge => Get(nameof(ButtonStartBridge));
    public static string ButtonStopBridge => Get(nameof(ButtonStopBridge));
    public static string ButtonSaveConfiguration => Get(nameof(ButtonSaveConfiguration));
    public static string CheckInvertAxis => Get(nameof(CheckInvertAxis));
    public static string CheckOverlay => Get(nameof(CheckOverlay));
    public static string CheckPowerRelease => Get(nameof(CheckPowerRelease));
    public static string CheckEmergencyRelease => Get(nameof(CheckEmergencyRelease));
    public static string ButtonLearnRelease => Get(nameof(ButtonLearnRelease));
    public static string ButtonOverlayPlace => Get(nameof(ButtonOverlayPlace));
    public static string ButtonOverlayPlaceDone => Get(nameof(ButtonOverlayPlaceDone));
    public static string ButtonLearnCancel => Get(nameof(ButtonLearnCancel));
    public static string HintAxis => Get(nameof(HintAxis));
    public static string HintModel => Get(nameof(HintModel));
    public static string LabelDeviceName => Get(nameof(LabelDeviceName));
    public static string HintDeviceName => Get(nameof(HintDeviceName));
    public static string HintButtonsInFile => Get(nameof(HintButtonsInFile));
    public static string GroupCatches => Get(nameof(GroupCatches));
    public static string GroupOverlay => Get(nameof(GroupOverlay));
    public static string HintOverlay => Get(nameof(HintOverlay));
    public static string HintCatches => Get(nameof(HintCatches));
    public static string HintReleaseNotSet => Get(nameof(HintReleaseNotSet));
    public static string HintReleaseBinding => Get(nameof(HintReleaseBinding));
    public static string HintReleasePress => Get(nameof(HintReleasePress));
    public static string ButtonEditBindings => Get(nameof(ButtonEditBindings));
    public static string DialogBindingsTitle => Get(nameof(DialogBindingsTitle));
    public static string DialogBindingsHint => Get(nameof(DialogBindingsHint));
    public static string LabelHat => Get(nameof(LabelHat));
    public static string HintHatBinding => Get(nameof(HintHatBinding));
    public static string HintHatPress => Get(nameof(HintHatPress));
    public static string HintBindingNone => Get(nameof(HintBindingNone));
    public static string ButtonClearBinding => Get(nameof(ButtonClearBinding));
    public static string ButtonDialogOk => Get(nameof(ButtonDialogOk));
    public static string ButtonDialogCancel => Get(nameof(ButtonDialogCancel));
    public static string HintEmergencyButton => Get(nameof(HintEmergencyButton));
    public static string LabelGame => Get(nameof(LabelGame));
    public static string GameNone => Get(nameof(GameNone));
    public static string GameJrEast => Get(nameof(GameJrEast));
    public static string HintGameDefaults => Get(nameof(HintGameDefaults));
    public static string FunctionAtsConfirmation => Get(nameof(FunctionAtsConfirmation));
    public static string FunctionHornLevel1 => Get(nameof(FunctionHornLevel1));
    public static string FunctionHornLevel2 => Get(nameof(FunctionHornLevel2));
    public static string FunctionDeadmanReset => Get(nameof(FunctionDeadmanReset));
    public static string FunctionAlarmStop => Get(nameof(FunctionAlarmStop));
    public static string FunctionSpeedSuppression1 => Get(nameof(FunctionSpeedSuppression1));
    public static string FunctionCruiseControl => Get(nameof(FunctionCruiseControl));
    public static string FunctionSwitchCabinView => Get(nameof(FunctionSwitchCabinView));
    public static string FunctionPauseGame => Get(nameof(FunctionPauseGame));
    public static string FunctionScreenshot => Get(nameof(FunctionScreenshot));
    public static string FunctionDoNotUse => Get(nameof(FunctionDoNotUse));
    public static string FunctionNotInManual => Get(nameof(FunctionNotInManual));
    public static string FunctionHat => Get(nameof(FunctionHat));
    public static string LabelVersion => Get(nameof(LabelVersion));
    public static string LinkUpdateAvailable => Get(nameof(LinkUpdateAvailable));
    public static string CheckForUpdates => Get(nameof(CheckForUpdates));
    public static string HintLanguageRestart => Get(nameof(HintLanguageRestart));
    public static string StatusStopped => Get(nameof(StatusStopped));
    public static string StatusBridgeRunning => Get(nameof(StatusBridgeRunning));
    public static string StatusCalibrating => Get(nameof(StatusCalibrating));
    public static string StatusNoMovement => Get(nameof(StatusNoMovement));
    public static string StatusConfigurationSaved => Get(nameof(StatusConfigurationSaved));
    public static string StatusFailedToStart => Get(nameof(StatusFailedToStart));
    public static string StatusNoJoystick => Get(nameof(StatusNoJoystick));
    public static string StatusDevicesMoved => Get(nameof(StatusDevicesMoved));
    public static string StatusDeviceMissing => Get(nameof(StatusDeviceMissing));
    public static string DeviceItem => Get(nameof(DeviceItem));
    public static string NotchSending => Get(nameof(NotchSending));
    public static string NotchPreview => Get(nameof(NotchPreview));
    public static string NotchHeldAtNeutral => Get(nameof(NotchHeldAtNeutral));
    public static string NotchHeldAtFullService => Get(nameof(NotchHeldAtFullService));
    public static string NotchAxisMissing => Get(nameof(NotchAxisMissing));
    public static string DialogCalibrationTitle => Get(nameof(DialogCalibrationTitle));
    public static string DialogCalibrationNoMovement => Get(nameof(DialogCalibrationNoMovement));
    public static string DialogStartTitle => Get(nameof(DialogStartTitle));
    public static string DialogStartFailed => Get(nameof(DialogStartFailed));
    public static string DialogSaveTitle => Get(nameof(DialogSaveTitle));
    public static string DialogSaveFailed => Get(nameof(DialogSaveFailed));
    public static string ConsoleUsage => Get(nameof(ConsoleUsage));
    public static string ConsoleUsageNoArgs => Get(nameof(ConsoleUsageNoArgs));
    public static string ConsoleListHeader => Get(nameof(ConsoleListHeader));
    public static string ConsoleNoJoystick => Get(nameof(ConsoleNoJoystick));
    public static string ConsoleJoystickLine => Get(nameof(ConsoleJoystickLine));
    public static string ConsoleAxisLine => Get(nameof(ConsoleAxisLine));
    public static string ConsoleButtonsLine => Get(nameof(ConsoleButtonsLine));
    public static string ConsolePovLine => Get(nameof(ConsolePovLine));
    public static string ConsoleCalibrating => Get(nameof(ConsoleCalibrating));
    public static string ConsoleCalibrateHint => Get(nameof(ConsoleCalibrateHint));
    public static string ConsoleCalibrateLive => Get(nameof(ConsoleCalibrateLive));
    public static string ConsoleCalibrateNoMovement => Get(nameof(ConsoleCalibrateNoMovement));
    public static string ConsoleCalibrateSaved => Get(nameof(ConsoleCalibrateSaved));
    public static string ConsoleCreatingDevice => Get(nameof(ConsoleCreatingDevice));
    public static string ConsoleTestReady => Get(nameof(ConsoleTestReady));
    public static string ConsoleTestCycling => Get(nameof(ConsoleTestCycling));
    public static string ConsoleTestNotch => Get(nameof(ConsoleTestNotch));
    public static string ConsoleRunHandle => Get(nameof(ConsoleRunHandle));
    public static string ConsoleRunInverted => Get(nameof(ConsoleRunInverted));
    public static string ConsoleRunZones => Get(nameof(ConsoleRunZones));
    public static string ConsoleRunVirtual => Get(nameof(ConsoleRunVirtual));
    public static string ConsoleRunLine => Get(nameof(ConsoleRunLine));
    public static string ConsoleStopped => Get(nameof(ConsoleStopped));
    public static string ConsoleDeviceMoved => Get(nameof(ConsoleDeviceMoved));
    public static string ConsoleDeviceMissing => Get(nameof(ConsoleDeviceMissing));
    public static string ConfigCreatedSample => Get(nameof(ConfigCreatedSample));
    public static string ConfigUnreadable => Get(nameof(ConfigUnreadable));
}

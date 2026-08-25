"""Generate Strings.resx / Strings.en.resx / Strings.ja.resx and Strings.cs.

One table, three files, so a key can never exist in one language and not another.
Run from the repository root.
"""

import os
from xml.sax.saxutils import escape

# key: (english, japanese)
S = {
    # --- window chrome -----------------------------------------------------
    "GroupDeviceAndAxis": ("Device and axis", "デバイスと軸"),
    "GroupCalibration": ("Calibration", "キャリブレーション"),
    "GroupVirtualDevice": ("Virtual device", "仮想デバイス"),
    "GroupCurrentNotch": ("Current notch", "現在のノッチ"),

    "LabelDevice": ("Device:", "デバイス:"),
    "LabelAxis": ("Axis:", "軸:"),
    "LabelMinimum": ("Minimum:", "最小:"),
    "LabelMaximum": ("Maximum:", "最大:"),
    "LabelHysteresis": ("Hysteresis:", "ヒステリシス:"),
    "LabelModel": ("Model:", "機種:"),
    "LabelLanguage": ("Language:", "言語:"),

    "ButtonRefresh": ("Refresh", "更新"),
    "ButtonCalibrate": ("Calibrate", "測定開始"),
    "ButtonCalibrateFinish": ("Finish", "測定終了"),
    "ButtonStartBridge": ("Start bridge", "ブリッジを開始"),
    "ButtonStopBridge": ("Stop bridge", "ブリッジを停止"),
    "ButtonSaveConfiguration": ("Save configuration", "設定を保存"),

    "CheckInvertAxis": ("Invert axis", "軸を反転"),
    "CheckOverlay": (
        "Show the notch over the game while the bridge runs",
        "ブリッジ動作中、ゲームの上にノッチを表示する",
    ),
    "CheckPowerRelease": ("Power, N to P1", "力行、N から P1"),
    "CheckEmergencyRelease": ("Emergency, B8 to EB", "非常、B8 から EB"),

    "ButtonLearnRelease": ("Choose button", "ボタンを選ぶ"),
    "ButtonOverlayPlace": ("Place it", "位置を決める"),
    "ButtonOverlayPlaceDone": ("Done", "完了"),
    "ButtonLearnCancel": ("Cancel", "中止"),

    # --- hints -------------------------------------------------------------
    "HintAxis": (
        "Move the handle and watch which of the six responds.",
        "ハンドルを動かし、6 つのうちどれが反応するか確認してください。",
    ),
    "HintModel": (
        "{0} is the default controller.\nOnly change this if the game ignores the mascon.",
        "{0} が既定のコントローラーです。\nゲームがマスコンを認識しない場合のみ変更してください。",
    ),
    "LabelDeviceName": ("Name:", "名前:"),
    "HintDeviceName": (
        "What the device calls itself. Leave it empty for mascon-bridge.\n"
        "The game matches on the ids and never reads this, but some third party "
        "tools -- a BVE input plugin, say -- look for the name of the real mascon.\n"
        "Takes effect the next time the bridge starts.",
        "デバイスが名乗る名前です。空欄なら mascon-bridge になります。\n"
        "ゲームは ID で判定するためこの名前を見ませんが、BVE の入力プラグインなど、"
        "実機の名前で探す外部ツールがあります。\n"
        "次にブリッジを開始したときから反映されます。",
    ),
    "HintButtonsInFile": ("Settings file:", "設定ファイル:"),
    "GroupCatches": ("Handle catches", "ハンドルの引っかかり"),
    "GroupOverlay": ("Overlay", "オーバーレイ"),
    "HintOverlay": (
        "The mouse goes straight through it while you play. Press Place it to drag it"
        " where you want, which shows it even with the bridge stopped.",
        "プレイ中はマウスがそのまま通り抜けます。「位置を決める」を押すとドラッグでき、"
        "ブリッジ停止中でも表示されます。",
    ),
    "HintCatches": (
        "Tick an end to make the handle need a button before it may cross into it, as"
        " the real mascon does. Only crossing is guarded: once across you can let go,"
        " and coming back sets the catch again.",
        "端にチェックを入れると、ハンドルがそこへ越えるときにボタンが必要になります。"
        "実車の親指ボタンと、非常側の押し込むカムにあたります。守られているのは越える"
        "ときだけで、越えたあとは離してかまいません。戻ればまた掛かります。",
    ),
    "HintReleaseNotSet": ("no button chosen", "ボタン未設定"),
    "HintReleaseBinding": ("device {0}, button {1}", "デバイス {0}・ボタン {1}"),
    "HintReleasePress": ("Press the button you want to use.", "使いたいボタンを押してください。"),

    # --- buttons and hat ---------------------------------------------------
    "ButtonEditBindings": ("Set buttons...", "ボタンを設定..."),
    "DialogBindingsTitle": ("Buttons and hat", "ボタンとハット"),
    "DialogBindingsHint": (
        "Pick a mascon button, then press the one you want on your hardware. "
        "A mascon button can carry more than one.",
        "マスコン側のボタンを選び、割り当てたいボタンを手元の機器で押してください。"
        "1 つのボタンに複数を割り当てられます。",
    ),
    "LabelHat": ("Hat", "ハット"),
    "HintHatBinding": ("device {0}", "デバイス {0}"),
    "HintHatPress": (
        "Press any button on the device whose hat you want.",
        "使いたいハットが付いている機器の、どれかのボタンを押してください。",
    ),
    "HintBindingNone": ("not assigned", "未割り当て"),
    "ButtonClearBinding": ("Clear", "消す"),
    "ButtonDialogOk": ("OK", "OK"),
    "ButtonDialogCancel": ("Cancel", "キャンセル"),
    "HintEmergencyButton": (
        "EB applies the emergency brake while it is held.",
        "EB は押している間だけ非常ブレーキがかかります。",
    ),

    "LabelVersion": ("version {0}", "バージョン {0}"),
    "LinkUpdateAvailable": ("version {0} is available", "バージョン {0} が公開されています"),
    "CheckForUpdates": ("Check for updates", "更新を確認する"),

    "HintLanguageRestart": (
        "The window reopens when you change this.",
        "変更するとウィンドウが開き直します。",
    ),

    # --- status ------------------------------------------------------------
    "StatusStopped": ("Stopped", "停止中"),
    "StatusBridgeRunning": ("Bridge running", "ブリッジ動作中"),
    "StatusCalibrating": (
        "Calibrating: move the handle end to end",
        "測定中: ハンドルを端から端まで動かしてください",
    ),
    "StatusNoMovement": ("No movement seen", "動きを検出できません"),
    "StatusConfigurationSaved": ("Configuration saved", "設定を保存しました"),
    "StatusFailedToStart": ("Failed to start", "開始できませんでした"),
    "StatusNoJoystick": ("No joystick detected", "ジョイスティックが見つかりません"),
    "StatusDevicesMoved": (
        "Joystick numbers changed, the settings followed",
        "ジョイスティック番号が変わりました。設定も追随しました",
    ),
    "StatusDeviceMissing": (
        "A joystick in the settings is not connected",
        "設定にあるジョイスティックが接続されていません",
    ),

    # --- readouts ----------------------------------------------------------
    "DeviceItem": (
        "{0} - {1} - {2} axes, {3} buttons",
        "{0} - {1} - {2} 軸, {3} ボタン",
    ),
    "NotchSending": (
        "axis {0}   ·   {1:F1}% of travel   ·   sending to the game",
        "軸 {0}   ·   全体の {1:F1}%   ·   ゲームへ送信中",
    ),
    "NotchPreview": (
        "axis {0}   ·   {1:F1}% of travel   ·   preview, the bridge is stopped",
        "軸 {0}   ·   全体の {1:F1}%   ·   プレビュー（ブリッジは停止中）",
    ),
    "NotchHeldAtNeutral": (
        "held at N   ·   press the button to enter power",
        "N で保持中   ·   力行に入るにはボタンを押してください",
    ),
    "NotchHeldAtFullService": (
        "held at B8   ·   press the button to reach emergency",
        "B8 で保持中   ·   非常ブレーキに入れるにはボタンを押してください",
    ),
    "NotchAxisMissing": (
        "the selected axis does not exist on this device",
        "選択した軸はこのデバイスにありません",
    ),

    # --- dialogs -----------------------------------------------------------
    "DialogCalibrationTitle": ("Calibration", "キャリブレーション"),
    "DialogCalibrationNoMovement": (
        "No movement was seen on the selected axis.\n\n"
        "Check the device and the axis: move the handle and watch which of the six "
        "values changes.",
        "選択した軸に動きがありませんでした。\n\n"
        "デバイスと軸を確認してください。ハンドルを動かし、6 つの値のどれが変化するか見てください。",
    ),
    "DialogStartTitle": ("Start bridge", "ブリッジの開始"),
    "DialogStartFailed": (
        "Could not create the virtual mascon.\n\n{0}: {1}\n\n"
        "If this is a permissions problem, run the program as administrator.",
        "仮想マスコンを作成できませんでした。\n\n{0}: {1}\n\n"
        "権限の問題であれば、管理者として実行してください。",
    ),
    "DialogSaveTitle": ("Save", "保存"),
    "DialogSaveFailed": (
        "Could not save:\n\n{0}",
        "保存できませんでした:\n\n{0}",
    ),

    # --- console -----------------------------------------------------------
    "ConsoleUsage": (
        "Usage: mascon-bridge [gui|list|calibrate|test|run]",
        "使い方: mascon-bridge [gui|list|calibrate|test|run]",
    ),
    "ConsoleUsageNoArgs": (
        "  with no arguments it opens the control window",
        "  引数なしで実行すると設定ウィンドウが開きます",
    ),
    "ConsoleListHeader": (
        "Move the handles and press buttons. Ctrl+C to quit.",
        "ハンドルを動かし、ボタンを押してください。Ctrl+C で終了します。",
    ),
    "ConsoleNoJoystick": ("No joystick detected.", "ジョイスティックが見つかりません。"),
    "ConsoleJoystickLine": (
        "Joystick {0}: {1}  {2} axes, {3} buttons",
        "ジョイスティック {0}: {1}  {2} 軸, {3} ボタン",
    ),
    "ConsoleAxisLine": ("   axis {0}: {1,6}  {2}", "   軸 {0}: {1,6}  {2}"),
    "ConsoleButtonsLine": ("   buttons: {0}", "   ボタン: {0}"),
    "ConsolePovLine": ("   POV: {0}", "   POV: {0}"),
    "ConsoleCalibrating": (
        "Calibrating joystick {0}, axis {1}.",
        "ジョイスティック {0}、軸 {1} を測定します。",
    ),
    "ConsoleCalibrateHint": (
        "Move the handle end to end a few times, then press Enter.",
        "ハンドルを端から端まで数回動かし、Enter を押してください。",
    ),
    "ConsoleCalibrateLive": (
        "  current {0,6}   min {1,6}   max {2,6}   ",
        "  現在 {0,6}   最小 {1,6}   最大 {2,6}   ",
    ),
    "ConsoleCalibrateNoMovement": (
        "No movement seen. Check the joystick number and axis with 'list'.",
        "動きを検出できません。'list' でジョイスティック番号と軸を確認してください。",
    ),
    "ConsoleCalibrateSaved": (
        "Saved: AxisMin={0}, AxisMax={1}",
        "保存しました: AxisMin={0}, AxisMax={1}",
    ),
    "ConsoleCreatingDevice": (
        "Creating virtual mascon  VID=0x{0:X4}  PID=0x{1:X4}  \"{2}\"",
        "仮想マスコンを作成します  VID=0x{0:X4}  PID=0x{1:X4}  「{2}」",
    ),
    "ConsoleTestReady": (
        "Ready. Open joy.cpl or Steam's controller test and watch the Y axis.",
        "準備完了。joy.cpl か Steam のコントローラーテストで Y 軸を確認してください。",
    ),
    "ConsoleTestCycling": (
        "Cycling the notches... Ctrl+C to quit.",
        "ノッチを順に送っています... Ctrl+C で終了します。",
    ),
    "ConsoleTestNotch": (
        "  notch {0,-3}  value 0x{1:X2}   ",
        "  ノッチ {0,-3}  値 0x{1:X2}   ",
    ),
    "ConsoleRunHandle": (
        "Handle : joystick {0}, axis {1}, range {2}..{3}{4}",
        "ハンドル: ジョイスティック {0}、軸 {1}、範囲 {2}..{3}{4}",
    ),
    "ConsoleRunInverted": (" (inverted)", "（反転）"),
    "ConsoleRunZones": ("Zones  : {0} ({1} .. P5)", "ゾーン  : {0} ({1} .. P5)"),
    "ConsoleRunVirtual": (
        "Virtual: VID=0x{0:X4} PID=0x{1:X4} \"{2}\"",
        "仮想    : VID=0x{0:X4} PID=0x{1:X4}「{2}」",
    ),
    "ConsoleRunLine": (
        "  {0,-3}  0x{1:X2}   buttons {2}",
        "  {0,-3}  0x{1:X2}   ボタン {2}",
    ),
    "ConsoleStopped": ("Stopped.", "停止しました。"),

    # --- devices moving about ----------------------------------------------
    "ConsoleDeviceMoved": (
        "Joystick {0} is now {1} ({2}); the settings followed it.",
        "ジョイスティック {0} は {1} になりました（{2}）。設定も追随しました。",
    ),
    "ConsoleDeviceMissing": (
        "Joystick {0} ({1}) is not connected; its number was left alone.",
        "ジョイスティック {0}（{1}）が接続されていません。番号はそのままにします。",
    ),

    # --- config ------------------------------------------------------------
    "ConfigCreatedSample": (
        "No config found, wrote a sample one to {0}",
        "設定が見つからないため、サンプルを {0} に作成しました",
    ),
    "ConfigUnreadable": (
        "config.json could not be read",
        "config.json を読み込めませんでした",
    ),
}

RESX_HEAD = '''<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
'''


def write_resx(path, index):
    parts = [RESX_HEAD]
    for key in S:
        value = S[key][index]
        parts.append('  <data name="%s" xml:space="preserve">\n    <value>%s</value>\n  </data>\n'
                     % (key, escape(value)))
    parts.append('</root>\n')
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(''.join(parts))
    print('wrote %s (%d strings)' % (path, len(S)))


def write_accessor(path):
    lines = [
        'using System.Globalization;',
        'using System.Resources;',
        '',
        'namespace MasconBridge;',
        '',
        '/// <summary>',
        '/// Every piece of text the program shows. Generated from one table, so a key',
        '/// cannot exist in one language and be missing from the other.',
        '///',
        '/// Which language is used comes from CultureInfo.CurrentUICulture, set at',
        '/// startup from the configuration.',
        '/// </summary>',
        'public static class Strings',
        '{',
        '    private static readonly ResourceManager Rm =',
        '        new("MasconBridge.Strings", typeof(Strings).Assembly);',
        '',
        '    private static string Get(string key) =>',
        '        Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;',
        '',
        '    /// <summary>Every key, for tests that check both languages are complete.</summary>',
        '    public static readonly string[] Keys =',
        '    {',
    ]
    for key in S:
        lines.append('        "%s",' % key)
    lines.append('    };')
    lines.append('')
    for key in S:
        lines.append('    public static string %s => Get(nameof(%s));' % (key, key))
    lines.append('}')
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')
    print('wrote %s (%d properties)' % (path, len(S)))


write_resx('Strings.resx', 0)
write_resx('Strings.en.resx', 0)
write_resx('Strings.ja.resx', 1)
write_accessor('Strings.cs')

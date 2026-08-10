# Guildrun MOD - Tools

Guildrun Demo 的工作台模組,提供除錯控制台與測試工具。

## 功能

- 🎮 **遊戲內控制台**:按 F1 開啟命令列
- 📊 **FPS 顯示**:即時監控效能
- 🎯 **測試命令**:15 個實用命令
- 🔌 **基於 Core**:使用 Guildrun-MOD-Core 提供的模組 API

## 系統需求

- Guildrun Demo (Steam)
- [BepInEx 6 IL2CPP](https://builds.bepinex.dev/projects/bepinex_be)
- [Guildrun-MOD-Core](https://github.com/alexanderedm/Guildrun-MOD-Core) ← 必須先裝

## 安裝

1. 確認已安裝 Core 模組
2. 下載 `build/plugins/Guildrun.Modules/Guildrun.MOD.Tools.dll`
3. 放到 `Guildrun Demo/BepInEx/plugins/Guildrun.Modules/`
4. 啟動遊戲,按 **F1** 開啟控制台

## 命令速查

| 命令 | 別名 | 說明 |
|---|---|---|
| `help` | `h` | 顯示所有命令 |
| `clear` | `cls` | 清除輸出 |
| `version` | - | 顯示 MOD 與遊戲版本 |
| `fps` | - | 切換 FPS 顯示 |
| `info` | - | 顯示遊戲資訊 |
| `difficulty <1-8>` | `diff` | 設定難度 |
| `gold <amount>` | - | 設定金幣 |
| `hp <amount>` | - | 設定 HP |
| `stage` | - | 顯示當前章節 |
| `event <id>` | - | 触发事件 |
| `exit` | `quit` | 關閉控制台 |

熱鍵:
- **F1**: 開啟/關閉控制台
- **↑ / ↓**: 瀏覽歷史記錄
- **Enter**: 執行命令

## 開源

歡迎貢獻!如需新增命令,編輯 `ToolsModule.cs` 中的 `RegisterCommands()` 與 `ExecuteCommand()`。

## 備註

此模組的「一鍵按鈕」中,**只有 Shards (金幣) 已驗證可運作**(透過 HarmonyX 攔截 `GameRunScope.Awake`)。
其他按鈕 (HP、難度、遺物等) 仍是 mock 輸出,實作需更多 reflection 修正。

如果想實際修改遊戲資料,**更可靠的方式**是使用:
👉 [Guildrun-SaveEditor](https://github.com/alexanderedm/Guildrun-SaveEditor) — 直接讀寫存檔,100% 可靠

## 🌐 Guildrun MOD 生態系

| 倉庫 | 用途 |
|---|---|
| [Guildrun-MOD-Core](https://github.com/alexanderedm/Guildrun-MOD-Core) | 模組核心 (必須) |
| [Guildrun-MOD-TraditionalChinese](https://github.com/alexanderedm/Guildrun-MOD-TraditionalChinese) | 繁中補強 |
| [Guildrun-MOD-Tools](https://github.com/alexanderedm/Guildrun-MOD-Tools) ← 本倉庫 | 遊戲內控制台 |
| [Guildrun-SaveEditor](https://github.com/alexanderedm/Guildrun-SaveEditor) | **Python 存檔編輯器** |

## 授權

MIT

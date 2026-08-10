using GuildrunMODCore;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GuildrunMODTools
{
    /// <summary>
    /// 工作台模組 - 除錯控制台 + 一鍵動作按鈕 + 滑鼠滾輪
    /// </summary>
    public class ToolsModule : ModuleBase
    {
        public override string Name => "Tools";
        public override string Version => "0.2.0";
        public override string Description => "除錯控制台 + 測試工具集 + 一鍵動作按鈕";
        public override string Author => "edmun";

        public KeyCode ToggleKey = KeyCode.F1;
        public bool ShowFps = true;

        public bool ConsoleOpen;
        public string Input = "";
        public List<string> Output = new();
        public List<string> CommandHistory = new();
        public int HistoryIndex;
        private CommandRegistry _commands = new();

        private int _scrollOffset;
        private const int OUTPUT_LINES = 22;

        private float _fps;
        private float _fpsAccum;
        private int _fpsFrames;
        private float _fpsLastUpdate;

        private GUIStyle _titleStyle;
        private GUIStyle _inputStyle;
        private GUIStyle _labelStyle;
        private bool _stylesInit;

        private static readonly KeyCode[] LetterKeys = {
            KeyCode.A, KeyCode.B, KeyCode.C, KeyCode.D, KeyCode.E, KeyCode.F, KeyCode.G, KeyCode.H,
            KeyCode.I, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.M, KeyCode.N, KeyCode.O, KeyCode.P,
            KeyCode.Q, KeyCode.R, KeyCode.S, KeyCode.T, KeyCode.U, KeyCode.V, KeyCode.W, KeyCode.X,
            KeyCode.Y, KeyCode.Z,
        };

        private static readonly KeyCode[] NumberKeys = {
            KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
        };

        private static readonly Dictionary<KeyCode, string> SymbolMap = new()
        {
            { KeyCode.Space, " " },
            { KeyCode.Minus, "-" },
            { KeyCode.Equals, "=" },
            { KeyCode.LeftBracket, "[" },
            { KeyCode.RightBracket, "]" },
            { KeyCode.Backslash, "\\" },
            { KeyCode.Semicolon, ";" },
            { KeyCode.Quote, "'" },
            { KeyCode.Comma, "," },
            { KeyCode.Period, "." },
            { KeyCode.Slash, "/" },
            { KeyCode.BackQuote, "`" },
        };

        protected override void OnInitialize()
        {
            RegisterCommands();
            PrintBanner();
            ScanGameTypes();

            // 套用 Harmony 補丁
            CurrentGameRunScopeSetterPatch.ApplyPatch();
        }

        private void ScanGameTypes()
        {
            // 初始化 GameReflection
            GameReflection.Initialize(_logger);

            // 套用 Harmony 補丁
            CurrentGameRunScopeSetterPatch.ApplyPatch();
            ProgressionServiceRunStartedPatch.ApplyPatch();
        }

        private void PrintBanner()
        {
            Print("╔══════════════════════════════════════════════╗");
            Print($"║   Guildrun MOD Tools v{Version,-22}║");
            Print("║   除錯控制台 · 測試按鈕 · 一鍵動作            ║");
            Print("╚══════════════════════════════════════════════╝");
            Print("");
            Print("【第一次使用?請看這裡】");
            Print("  1. 按 F1 開啟控制台 (隨時可按)");
            Print("  2. 看到下方按鈕列:用滑鼠點擊就能一鍵動作");
            Print("  3. 想輸入命令:直接打字後按 Enter");
            Print("  4. 不知道能幹嘛:輸入 'help' 或 'intro'");
            Print("");
            Print("【熱鍵速查】");
            Print("  F1       開啟/關閉控制台");
            Print("  滑鼠滾輪  捲動輸出(移到控制台內捲)");
            Print("  Enter    執行命令");
            Print("  ↑↓       切換歷史記錄");
            Print("  Ctrl+L   清除輸出畫面");
            Print("  Esc      關閉控制台");
            Print("");
            Print("─────────────────────────────────────────────────");
            Print("試試點擊上方 [+1000G] [HP全滿] [難度1] 按鈕!");
            Print("或輸入 'help' 查看所有命令");
            Print("");
        }

        private void RegisterCommands()
        {
            _commands.Register("help", "顯示所有命令(輸入 help <cmd> 看詳細)", HelpCommand);
            _commands.Register("h", "help 別名", HelpCommand);
            _commands.Register("?", "help 別名", HelpCommand);
            _commands.Register("intro", "重新顯示歡迎頁", IntroCommand);
            _commands.Register("clear", "清除輸出畫面", ClearCommand);
            _commands.Register("cls", "clear 別名", ClearCommand);

            _commands.Register("version", "顯示 MOD 與 Unity 版本", VersionCommand);
            _commands.Register("info", "顯示遊戲資訊", InfoCommand);
            _commands.Register("fps", "切換 FPS 顯示", FpsCommand);
            _commands.Register("modlist", "列出已載入的模組", ModListCommand);

            _commands.Register("hp", "設定 HP:hp <數值>", HpCommand);
            _commands.Register("gold", "設定金幣:gold <數值>", GoldCommand);
            _commands.Register("class", "切換職業:class <id>", ClassCommand);
            _commands.Register("relics", "列出持有遺物", RelicsCommand);
            _commands.Register("items", "列出持有物品", ItemsCommand);

            _commands.Register("diff", "設定難度:diff <1-8>", DifficultyCommand);
            _commands.Register("difficulty", "diff 別名", DifficultyCommand);
            _commands.Register("stage", "顯示當前章節", StageCommand);
            _commands.Register("event", "触发事件:event <id>", EventCommand);
            _commands.Register("node", "列出地圖節點", NodeCommand);

            _commands.Register("kill", "殺死當前戰鬥所有敵人", KillCommand);
            _commands.Register("skip", "跳過當前章節", SkipCommand);
            _commands.Register("maxall", "一鍵全資源最大化", MaxAllCommand);

            _commands.Register("exit", "關閉控制台", ExitCommand);
            _commands.Register("quit", "exit 別名", ExitCommand);
        }

        public override void OnUpdate()
        {
            _fpsAccum += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (Time.unscaledTime - _fpsLastUpdate > 0.5f)
            {
                _fps = _fpsFrames / _fpsAccum;
                _fpsAccum = 0;
                _fpsFrames = 0;
                _fpsLastUpdate = Time.unscaledTime;
            }

            if (UnityEngine.Input.GetKeyDown(ToggleKey))
            {
                ConsoleOpen = !ConsoleOpen;
                if (ConsoleOpen)
                {
                    Input = "";
                    HistoryIndex = CommandHistory.Count;
                    _scrollOffset = 0;
                }
            }

            if (ConsoleOpen)
            {
                PollKeyboard();
            }
        }

        private void PollKeyboard()
        {
            bool shift = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);

            foreach (var k in LetterKeys)
            {
                if (UnityEngine.Input.GetKeyDown(k))
                {
                    char c = k.ToString()[0];
                    Input += shift ? c : char.ToLowerInvariant(c);
                }
            }

            foreach (var k in NumberKeys)
            {
                if (UnityEngine.Input.GetKeyDown(k))
                {
                    int digit = (int)k - (int)KeyCode.Alpha0;
                    Input += shift ? ShiftDigit(digit) : digit.ToString();
                }
            }

            foreach (var kv in SymbolMap)
            {
                if (UnityEngine.Input.GetKeyDown(kv.Key))
                {
                    string s = kv.Value;
                    if (shift) s = ShiftSymbol(s);
                    Input += s;
                }
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Backspace) && Input.Length > 0)
                Input = Input.Substring(0, Input.Length - 1);

            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (!string.IsNullOrEmpty(Input.Trim()))
                {
                    ExecuteCommand(Input.Trim());
                    CommandHistory.Add(Input.Trim());
                    HistoryIndex = CommandHistory.Count;
                }
                Input = "";
                _scrollOffset = 0;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (CommandHistory.Count > 0)
                {
                    HistoryIndex = Math.Max(0, HistoryIndex - 1);
                    Input = CommandHistory[HistoryIndex];
                }
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (CommandHistory.Count > 0)
                {
                    HistoryIndex = Math.Min(CommandHistory.Count, HistoryIndex + 1);
                    Input = HistoryIndex < CommandHistory.Count ? CommandHistory[HistoryIndex] : "";
                }
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                ConsoleOpen = false;

            if (UnityEngine.Input.GetKeyDown(KeyCode.L) &&
                (UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl)))
            {
                Output.Clear();
                _scrollOffset = 0;
            }
        }

        private static string ShiftDigit(int d) => d switch
        {
            0 => ")", 1 => "!", 2 => "@", 3 => "#", 4 => "$",
            5 => "%", 6 => "^", 7 => "&", 8 => "*", 9 => "(",
            _ => d.ToString(),
        };

        private static string ShiftSymbol(string s) => s switch
        {
            "-" => "_", "=" => "+", "[" => "{", "]" => "}",
            "\\" => "|", ";" => ":", "'" => "\"", "," => "<",
            "." => ">", "/" => "?", "`" => "~",
            _ => s,
        };

        public override void OnGUI()
        {
            EnsureStyles();

            if (ShowFps)
            {
                var rect = new Rect(Screen.width - 130, 10, 120, 30);
                GUI.Box(rect, $"FPS: {_fps:F1}");
            }

            if (ConsoleOpen)
            {
                DrawConsole();
            }
        }

        private void DrawConsole()
        {
            var consoleRect = new Rect(50, 50, Screen.width - 100, Screen.height * 0.55f);
            GUI.Box(consoleRect, "");

            // 滑鼠滾輪捲動
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0 && consoleRect.Contains(UnityEngine.Input.mousePosition))
            {
                _scrollOffset += scroll > 0 ? -3 : 3;
                int maxOff = Math.Max(0, Output.Count - OUTPUT_LINES);
                _scrollOffset = Math.Clamp(_scrollOffset, 0, maxOff);
            }

            float x = 60;
            float y = 60;
            float w = consoleRect.width - 20;

            // 標題
            GUI.Label(new Rect(x, y, w, 26),
                $"Guildrun MOD Tools v{Version}  [F1/Esc 關閉] [滾輪:捲動]",
                _titleStyle);
            y += 30;

            // 一鍵動作按鈕列
            DrawQuickActions(x, y, w);
            y += 38;

            // 分隔線
            GUI.Box(new Rect(x, y, w, 1), "");
            y += 4;

            // 輸出區(手動捲動)
            int maxOffsetLines = Math.Max(0, Output.Count - OUTPUT_LINES);
            int startIdx = maxOffsetLines - _scrollOffset;
            startIdx = Math.Max(0, startIdx);
            int endIdx = Math.Min(Output.Count, startIdx + OUTPUT_LINES);

            for (int i = startIdx; i < endIdx; i++)
            {
                string line = StripRichText(Output[i]);
                Color oldColor = GUI.color;
                if (Output[i].Contains("FF6060")) GUI.color = new Color(1f, 0.4f, 0.4f);
                else if (Output[i].Contains("FFB060")) GUI.color = new Color(1f, 0.7f, 0.4f);
                else if (Output[i].Contains("60FF60")) GUI.color = new Color(0.4f, 1f, 0.4f);
                GUI.Label(new Rect(x, y, w, 18), line, _labelStyle);
                GUI.color = oldColor;
                y += 18;
            }

            // 捲動指示
            string scrollHint = Output.Count > OUTPUT_LINES ? $"  [{startIdx + 1}-{endIdx}/{Output.Count}]" : "";
            GUI.Label(new Rect(consoleRect.x + consoleRect.width - 200, consoleRect.y + 8, 190, 20),
                $"行:{Output.Count}{scrollHint}", _labelStyle);

            // 輸入列
            float inputY = consoleRect.y + consoleRect.height - 30;
            string cursor = Time.unscaledTime % 1.0f < 0.5f ? "_" : " ";
            GUI.Label(new Rect(x, inputY, 25, 25), ">", _inputStyle);
            GUI.Label(new Rect(x + 22, inputY, w - 30, 25), Input + cursor, _inputStyle);
        }

        private void DrawQuickActions(float x, float y, float w)
        {
            float btnW = 92;
            float btnH = 32;
            float gap = 4;
            float cx = x;

            ButtonAt(ref cx, y, btnW, btnH, gap, "+1000G", () => ExecuteCommand("gold 1000"));
            ButtonAt(ref cx, y, btnW, btnH, gap, "+9999G", () => ExecuteCommand("gold 9999"));
            ButtonAt(ref cx, y, btnW, btnH, gap, "HP全滿", () => ExecuteCommand("hp 9999"));
            ButtonAt(ref cx, y, btnW, btnH, gap, "難度1", () => ExecuteCommand("diff 1"));
            ButtonAt(ref cx, y, btnW, btnH, gap, "難度5", () => ExecuteCommand("diff 5"));
            ButtonAt(ref cx, y, btnW, btnH, gap, "難度8", () => ExecuteCommand("diff 8"));
            ButtonAt(ref cx, y, btnW, btnH, gap, "給遺物", () => ExecuteCommand("give_relic Relic_FlameHeart"));
            ButtonAt(ref cx, y, btnW, btnH, gap, "一鍵全滿", () => ExecuteCommand("maxall"));
            ButtonAt(ref cx, y, btnW, btnH, gap, "跳關", () => ExecuteCommand("skip"));
            ButtonAt(ref cx, y, btnW, btnH, gap, "殺敵", () => ExecuteCommand("kill"));
            ButtonAt(ref cx, y, btnW, btnH, gap, "modlist", () => ExecuteCommand("modlist"));
            ButtonAt(ref cx, y, btnW, btnH, gap, "清輸出", () => ExecuteCommand("cls"));
        }

        private void ButtonAt(ref float cx, float y, float w, float h, float gap, string label, Action onClick)
        {
            if (GUI.Button(new Rect(cx, y, w, h), label))
            {
                onClick?.Invoke();
            }
            cx += w + gap;
        }

        private static string StripRichText(string s)
        {
            return s.Replace("<color=#FFFFFF>", "")
                    .Replace("<color=#FF6060>", "")
                    .Replace("<color=#FFB060>", "")
                    .Replace("<color=#60FF60>", "")
                    .Replace("<color=#FF8040>", "")
                    .Replace("<color=#60C0FF>", "")
                    .Replace("</color>", "");
        }

        private void EnsureStyles()
        {
            if (_stylesInit) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.5f, 0.9f, 1f) },
            };
            _inputStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.98f, 1f) },
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.95f, 0.95f, 0.95f) },
            };
            _stylesInit = true;
        }

        private void ExecuteCommand(string cmd)
        {
            Print($"> {cmd}");
            var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            var handler = _commands.Find(parts[0]);
            if (handler == null)
            {
                PrintError($"未知命令: {parts[0]}");
                Print("輸入 'help' 看所有命令,或 'intro' 重新看歡迎頁");
                return;
            }
            try
            {
                handler(parts);
            }
            catch (Exception ex)
            {
                PrintError($"執行失敗: {ex.Message}");
            }
        }

        // === 命令實作 ===
        private void IntroCommand(string[] args)
        {
            Output.Clear();
            PrintBanner();
        }

        private void HelpCommand(string[] args)
        {
            if (args.Length > 1)
            {
                var cmd = _commands.FindCommand(args[1]);
                if (cmd == null)
                {
                    PrintError($"找不到命令: {args[1]}");
                    return;
                }
                Print("┌─────────────────────────────────────────┐");
                Print($"│ 命令: {cmd.Name}");
                Print($"│ 說明: {cmd.Description}");
                Print("└─────────────────────────────────────────┘");
                return;
            }

            Print("═══ 幫助與資訊 ═══");
            foreach (var cmd in _commands.GetAll())
            {
                if (cmd.Name == "help" || cmd.Name == "h" || cmd.Name == "?" ||
                    cmd.Name == "intro" || cmd.Name == "clear" || cmd.Name == "cls" ||
                    cmd.Name == "version" || cmd.Name == "info" || cmd.Name == "fps" ||
                    cmd.Name == "modlist" || cmd.Name == "exit" || cmd.Name == "quit")
                    Print($"  {cmd.Name,-12} {cmd.Description}");
            }
            Print("");
            Print("═══ 玩家與資源 ═══");
            foreach (var cmd in _commands.GetAll())
            {
                if (cmd.Name == "hp" || cmd.Name == "gold" || cmd.Name == "class" ||
                    cmd.Name == "relics" || cmd.Name == "items")
                    Print($"  {cmd.Name,-12} {cmd.Description}");
            }
            Print("");
            Print("═══ 章節事件 ═══");
            foreach (var cmd in _commands.GetAll())
            {
                if (cmd.Name == "diff" || cmd.Name == "difficulty" || cmd.Name == "stage" ||
                    cmd.Name == "event" || cmd.Name == "node")
                    Print($"  {cmd.Name,-12} {cmd.Description}");
            }
            Print("");
            Print("═══ 危險命令 ═══");
            foreach (var cmd in _commands.GetAll())
            {
                if (cmd.Name == "kill" || cmd.Name == "skip" || cmd.Name == "maxall")
                    Print($"  {cmd.Name,-12} {cmd.Description}");
            }
            Print("");
            Print("─────────────────────────────────────────────────");
            Print("輸入 'help <命令>' 看單一命令詳細說明");
            Print("輸入 'intro' 重新顯示歡迎頁");
            _scrollOffset = 0;
        }

        private void ClearCommand(string[] args)
        {
            Output.Clear();
            _scrollOffset = 0;
        }

        private void VersionCommand(string[] args)
        {
            Print("═══ 版本資訊 ═══");
            Print($"  Guildrun MOD Tools:  v{Version}");
            Print($"  Unity Engine:        {Application.unityVersion}");
            Print($"  平台:                {Application.platform}");
            Print($"  解析度:              {Screen.width} x {Screen.height}");
            Print($"  .NET Runtime:        6.0.7");
            Print($"  IL2CPP:              啟用");
        }

        private void FpsCommand(string[] args)
        {
            ShowFps = !ShowFps;
            Print($"FPS 顯示: {(ShowFps ? "開啟" : "關閉")}");
        }

        private void InfoCommand(string[] args)
        {
            Print("═══ 遊戲資訊 ═══");
            Print($"  Unity:    {Application.unityVersion}");
            Print($"  平台:    {Application.platform}");
            Print($"  解析度:  {Screen.width} x {Screen.height}");
            Print($"  FPS:     {_fps:F1}");
            Print($"  系統時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        private void HpCommand(string[] args)
        {
            if (args.Length < 2) { Print("用法: hp <數值> (範例: hp 100)"); return; }
            if (int.TryParse(args[1], out var v)) PrintSuccess($"✓ HP 設定為 {v}");
            else PrintError($"'{args[1]}' 不是數字");
        }

        private void GoldCommand(string[] args)
        {
            if (args.Length < 2) { Print("用法: gold <數值> (範例: gold 9999)"); return; }
            if (int.TryParse(args[1], out var v))
            {
                if (GameReflection.SetGold(v))
                    PrintSuccess($"✓ Shards 設定為 {v}");
                else
                {
                    PrintError("✗ 設定失敗!");
                    Print("  提示1:請先開始一個 RUN(進入戰鬥畫面)");
                    Print("  提示2:遊戲中稱為 Shards(碎片),不是 Gold");
                    Print("  提示3:輸出視窗的提示訊息會說明細節");
                }
            }
            else PrintError($"'{args[1]}' 不是數字");
        }

        private void ClassCommand(string[] args)
        {
            if (args.Length < 2) { Print("用法: class <職業id>"); return; }
            PrintSuccess($"✓ 切換職業到 {args[1]}");
        }

        private void RelicsCommand(string[] args)
        {
            Print("═══ 持有遺物 (示範) ═══");
            Print("  1. 烈焰之心 (+5 攻擊)");
            Print("  2. 鐵壁肌膚 (+10 防禦)");
            Print("  3. 幸運符 (+5% 暴擊率)");
        }

        private void ItemsCommand(string[] args)
        {
            Print("═══ 持有物品 (示範) ═══");
            Print("  1. 治療藥水 x3");
            Print("  2. 魔力藥水 x2");
            Print("  3. 鑰匙 x1");
        }

        private void DifficultyCommand(string[] args)
        {
            if (args.Length < 2) { Print("用法: diff <1-8>"); return; }
            if (int.TryParse(args[1], out var level) && level >= 1 && level <= 8)
                PrintSuccess($"✓ 難度設定為 {level}");
            else
                PrintError("難度必須在 1-8 之間");
        }

        private void StageCommand(string[] args)
        {
            Print("═══ 當前章節 (示範) ═══");
            Print("  Act:    1");
            Print("  Stage:  3");
            Print("  節點:   5/8 已完成");
            Print("  下一節: 戰鬥");
        }

        private void EventCommand(string[] args)
        {
            if (args.Length < 2) { Print("用法: event <id>"); return; }
            PrintSuccess($"✓ 触发事件: {args[1]}");
        }

        private void NodeCommand(string[] args)
        {
            Print("═══ 當前地圖節點 (示範) ═══");
            Print("  [1] 戰鬥   ✓");
            Print("  [2] 戰鬥   ✓");
            Print("  [3] 事件   ◉");
            Print("  [4] 商店   ○");
            Print("  [5] 休息   ○");
            Print("  [6] 菁英   ○");
            Print("  [7] 寶藏   ○");
            Print("  [8] 首領   ○");
        }

        private void KillCommand(string[] args) => PrintSuccess("✓ 殺死當前戰鬥所有敵人");
        private void SkipCommand(string[] args) => PrintSuccess("✓ 跳過當前章節");

        private void MaxAllCommand(string[] args)
        {
            PrintSuccess("═══ 一鍵全滿執行 ═══");
            PrintSuccess("✓ HP: 9999");
            PrintSuccess("✓ 金幣: 99999");
            PrintSuccess("✓ 攻擊力: 999");
            PrintSuccess("✓ 防禦力: 999");
            PrintSuccess("✓ 難度: 8");
        }

        private void ModListCommand(string[] args)
        {
            Print("═══ 已載入模組 ═══");
            foreach (var mod in CorePlugin.Modules)
                Print($"  ● {mod.Name} v{mod.Version}");
        }

        private void ExitCommand(string[] args) => ConsoleOpen = false;

        // === 輸出 ===
        public void Print(string s)
        {
            Output.Add($"<color=#FFFFFF>{s}</color>");
            TrimOutput();
        }

        public void PrintError(string s)
        {
            Output.Add($"<color=#FF6060>{s}</color>");
            TrimOutput();
        }

        public void PrintWarning(string s)
        {
            Output.Add($"<color=#FFB060>{s}</color>");
            TrimOutput();
        }

        public void PrintSuccess(string s)
        {
            Output.Add($"<color=#60FF60>{s}</color>");
            TrimOutput();
        }

        private void TrimOutput()
        {
            const int MAX = 500;
            if (Output.Count > MAX)
                Output.RemoveRange(0, Output.Count - MAX);
            _scrollOffset = 0;
        }
    }

    public class CommandRegistry
    {
        public class Command
        {
            public string Name;
            public string Description;
            public Action<string[]> Handler;
        }

        private Dictionary<string, Command> _cmds = new();

        public void Register(string name, string desc, Action<string[]> handler)
        {
            _cmds[name] = new Command { Name = name, Description = desc, Handler = handler };
        }

        public Action<string[]> Find(string name)
        {
            return _cmds.TryGetValue(name, out var c) ? c.Handler : null;
        }

        public Command FindCommand(string name)
        {
            return _cmds.TryGetValue(name, out var c) ? c : null;
        }

        public IEnumerable<Command> GetAll() => _cmds.Values;
    }
}

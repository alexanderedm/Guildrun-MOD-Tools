using GuildrunMODCore;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GuildrunMODTools
{
    /// <summary>
    /// 工作台模組 - 除錯控制台與測試工具
    ///
    /// 注意:由於 Unity 6 + 新 Input System 下 GUI.TextField 經常無法接收鍵盤,
    /// 本模組改用 Input.GetKeyDown 直接輪詢每個按鍵。
    /// </summary>
    public class ToolsModule : ModuleBase
    {
        public override string Name => "Tools";
        public override string Version => "0.1.0";
        public override string Description => "除錯控制台 + 測試工具集";
        public override string Author => "edmun";

        // 設定
        public bool ConsoleEnabled = true;
        public KeyCode ToggleKey = KeyCode.F1;
        public bool ShowFps = true;

        // 狀態
        public bool ConsoleOpen;
        public string Input = "";
        public List<string> Output = new();
        public List<string> CommandHistory = new();
        public int HistoryIndex;
        private CommandRegistry _commands = new();
        private Vector2 _scrollPos;

        // FPS
        private float _fps;
        private float _fpsAccum;
        private int _fpsFrames;
        private float _fpsLastUpdate;

        // 樣式
        private GUIStyle _inputStyle;
        private bool _stylesInit;

        // 按鍵輪詢 - 避免重複觸發
        private readonly HashSet<KeyCode> _keysDown = new();

        // 已輪詢的字母鍵盤
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

        // 符號對映
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
            Print("=== Guildrun MOD Tools 已啟動 ===");
            Print("按 F1 開啟/關閉控制台");
            Print("輸入 'help' 查看所有命令");
        }

        private void RegisterCommands()
        {
            _commands.Register("help", "顯示命令列表", HelpCommand);
            _commands.Register("h", "顯示命令列表", HelpCommand);
            _commands.Register("clear", "清除輸出", ClearCommand);
            _commands.Register("cls", "清除輸出", ClearCommand);
            _commands.Register("version", "顯示版本", VersionCommand);
            _commands.Register("fps", "切換 FPS 顯示", FpsCommand);
            _commands.Register("info", "顯示遊戲資訊", InfoCommand);
            _commands.Register("difficulty", "語法:difficulty <1-8>", DifficultyCommand);
            _commands.Register("diff", "語法:diff <1-8>", DifficultyCommand);
            _commands.Register("gold", "語法:gold <amount>", GoldCommand);
            _commands.Register("hp", "語法:hp <amount>", HpCommand);
            _commands.Register("stage", "顯示當前章節", StageCommand);
            _commands.Register("event", "語法:event <event_id>", EventCommand);
            _commands.Register("modlist", "列出已載入模組", ModListCommand);
            _commands.Register("exit", "關閉控制台", ExitCommand);
            _commands.Register("quit", "關閉控制台", ExitCommand);
        }

        public override void OnUpdate()
        {
            // FPS 計算
            _fpsAccum += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (Time.unscaledTime - _fpsLastUpdate > 0.5f)
            {
                _fps = _fpsFrames / _fpsAccum;
                _fpsAccum = 0;
                _fpsFrames = 0;
                _fpsLastUpdate = Time.unscaledTime;
            }

            // F1 切換(toggle)
            if (UnityEngine.Input.GetKeyDown(ToggleKey))
            {
                ConsoleOpen = !ConsoleOpen;
                if (ConsoleOpen)
                {
                    Input = "";
                    HistoryIndex = CommandHistory.Count;
                }
            }

            // 控制台開啟時輪詢鍵盤輸入
            if (ConsoleOpen)
            {
                PollKeyboard();
            }
        }

        /// <summary>
        /// 直接輪詢每個按鍵,完全繞過 GUI.TextField
        /// </summary>
        private void PollKeyboard()
        {
            // Shift 偵測
            bool shift = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);

            // 字母
            foreach (var k in LetterKeys)
            {
                if (UnityEngine.Input.GetKeyDown(k))
                {
                    char c = k.ToString()[0];
                    Input += shift ? c : char.ToLowerInvariant(c);
                }
            }

            // 數字
            foreach (var k in NumberKeys)
            {
                if (UnityEngine.Input.GetKeyDown(k))
                {
                    int digit = (int)k - (int)KeyCode.Alpha0;
                    Input += shift ? ShiftDigit(digit) : digit.ToString();
                }
            }

            // 符號
            foreach (var kv in SymbolMap)
            {
                if (UnityEngine.Input.GetKeyDown(kv.Key))
                {
                    string s = kv.Value;
                    if (shift) s = ShiftSymbol(s);
                    Input += s;
                }
            }

            // Backspace
            if (UnityEngine.Input.GetKeyDown(KeyCode.Backspace) && Input.Length > 0)
            {
                Input = Input.Substring(0, Input.Length - 1);
            }

            // Enter - 執行
            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (!string.IsNullOrEmpty(Input.Trim()))
                {
                    ExecuteCommand(Input.Trim());
                    CommandHistory.Add(Input.Trim());
                    HistoryIndex = CommandHistory.Count;
                }
                Input = "";
            }

            // 上下鍵 - 歷史
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

            // Esc - 關閉
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                ConsoleOpen = false;
            }

            // Ctrl+L - 清除
            if (UnityEngine.Input.GetKeyDown(KeyCode.L) &&
                (UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl)))
            {
                Output.Clear();
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

            // FPS 顯示
            if (ShowFps)
            {
                var rect = new Rect(Screen.width - 130, 10, 120, 30);
                GUI.Box(rect, $"FPS: {_fps:F1}");
            }

            // 控制台
            if (ConsoleOpen)
            {
                DrawConsole();
            }
        }

        private void DrawConsole()
        {
            var consoleRect = new Rect(50, 50, Screen.width - 100, Screen.height * 0.5f);
            GUI.Box(consoleRect, "Guildrun Debug Console  [F1 關閉]");

            // 直接用 Rect + GUI.Label 顯示,不用 BeginScrollView
            float y = 85;
            float maxY = consoleRect.height - 50;
            int maxLines = 30;
            int startIdx = Math.Max(0, Output.Count - maxLines);

            for (int i = startIdx; i < Output.Count; i++)
            {
                // 移除 rich text 避免解析錯誤
                string line = Output[i].Replace("<color=#FFFFFF>", "").Replace("<color=#FF6060>", "").Replace("<color=#FFB060>", "").Replace("<color=#60FF60>", "").Replace("</color>", "");
                GUI.Label(new Rect(60, y, consoleRect.width - 20, 20), line);
                y += 18;
                if (y > 85 + maxY - 40) break;
            }

            // 輸入列
            string cursor = Time.unscaledTime % 1.0f < 0.5f ? "_" : " ";
            string inputLine = "> " + Input + cursor;
            GUI.Label(new Rect(60, consoleRect.height - 5, consoleRect.width - 20, 25), inputLine, _inputStyle);
        }

        private void EnsureStyles()
        {
            if (_stylesInit) return;
            _inputStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.9f, 0.95f, 1f) },
                fontStyle = FontStyle.Bold,
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
                Print("輸入 'help' 查看所有命令");
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
        private void HelpCommand(string[] args)
        {
            Print("=== 可用命令 ===");
            foreach (var cmd in _commands.GetAll())
            {
                Print($"  {cmd.Name,-12} {cmd.Description}");
            }
            Print("");
            Print("提示:直接打字輸入,Enter 執行,↑↓ 歷史,Esc 關閉");
        }

        private void ClearCommand(string[] args) => Output.Clear();

        private void VersionCommand(string[] args)
        {
            Print($"Guildrun MOD Tools v{Version}");
            Print($"Unity 版本: {Application.unityVersion}");
            Print($"平台: {Application.platform}");
        }

        private void FpsCommand(string[] args)
        {
            ShowFps = !ShowFps;
            Print($"FPS 顯示: {(ShowFps ? "開" : "關")}");
        }

        private void InfoCommand(string[] args)
        {
            Print($"=== 遊戲資訊 ===");
            Print($"  Unity: {Application.unityVersion}");
            Print($"  平台: {Application.platform}");
            Print($"  解析度: {Screen.width}x{Screen.height}");
            Print($"  FPS: {_fps:F1}");
            Print($"  系統時間: {DateTime.Now}");
        }

        private void DifficultyCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Print("用法: difficulty <1-8>");
                return;
            }
            if (int.TryParse(args[1], out var level) && level >= 1 && level <= 8)
            {
                Print($"✓ 難度設定為 {level} (示範)");
            }
            else
            {
                PrintError("難度必須在 1-8 之間");
            }
        }

        private void GoldCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Print("用法: gold <amount>");
                return;
            }
            if (int.TryParse(args[1], out var amount))
            {
                Print($"✓ 金幣設定為 {amount} (示範)");
            }
        }

        private void HpCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Print("用法: hp <amount>");
                return;
            }
            if (int.TryParse(args[1], out var v))
            {
                Print($"✓ HP 設定為 {v} (示範)");
            }
        }

        private void StageCommand(string[] args)
        {
            Print("當前章節: (示範)");
            Print("  Act 1 / Stage 3");
            Print("  剩餘節點: 4");
            Print("  下一節點類型: Battle");
        }

        private void EventCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Print("用法: event <event_id>");
                Print("已知事件: Event_500, Event_501, Event_502, Event_701, Event_1000, MrBigEvent");
                return;
            }
            Print($"✓ 触发事件: {args[1]} (示範)");
        }

        private void ModListCommand(string[] args)
        {
            Print("=== 已載入模組 ===");
            foreach (var mod in CorePlugin.Modules)
            {
                Print($"  • {mod.Name} v{mod.Version}");
            }
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
            const int MAX = 200;
            if (Output.Count > MAX)
                Output.RemoveRange(0, Output.Count - MAX);
            _scrollPos.y = float.MaxValue;
        }
    }

    /// <summary>
    /// 簡單命令註冊
    /// </summary>
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

        public IEnumerable<Command> GetAll() => _cmds.Values;
    }
}

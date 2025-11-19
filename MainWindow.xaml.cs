using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PsConsoleHost.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace PsConsoleHost
{
    public partial class MainWindow : Window
    {
        private Process? _proc;
        private bool _closing;
        private string? _currentPath;   // script ouvert/enregistré

        // Historique des commandes saisies dans la zone d'entrée
        private readonly List<string> _history = new();
        private int _historyIndex = -1;
        

        public MainWindow()
        {
            InitializeComponent();
            
            // Afficher la version dans le titre
            UpdateWindowTitle();

            try
            {
                using var s = Application.GetResourceStream(
                    new Uri("PowerShell.xshd", UriKind.Relative))?.Stream;
                if (s != null)
                {
                    using var xr = new XmlTextReader(s);
                    Editor.SyntaxHighlighting = HighlightingLoader.Load(xr, HighlightingManager.Instance);
                }
            }
            catch
            {
                // Ignore si la coloration ne peut pas être chargée
            }

            Loaded += (_, __) =>
            {
                StartPwsh();
                Input.Focus();
            };

            Closed += (_, __) =>
            {
                _closing = true;
                try { _proc?.Kill(entireProcessTree: true); } catch { /* ignore */ }
            };

            // Raccourcis pratiques
            InputBindings.Add(new KeyBinding(new RelayCommand(RunFile), new KeyGesture(Key.F5)));
            InputBindings.Add(new KeyBinding(new RelayCommand(RunSelection), new KeyGesture(Key.F8)));
            InputBindings.Add(new KeyBinding(new RelayCommand(OpenScript), new KeyGesture(Key.O, ModifierKeys.Control)));
            InputBindings.Add(new KeyBinding(new RelayCommand(SaveScript), new KeyGesture(Key.S, ModifierKeys.Control)));
            InputBindings.Add(new KeyBinding(new RelayCommand(() => Clear_Click(null!, null!)), new KeyGesture(Key.L, ModifierKeys.Control))); // Ctrl+L clear
        }

        #region Lancement pwsh & I/O

        /// <summary>
        /// Lance pwsh.exe avec -NoExit et branche les flux sur l'UI
        /// </summary>
        private void StartPwsh(string? scriptPath = null, string? args = null)
        {
            if (_proc is { HasExited: false })
            {
                try { _proc.Kill(entireProcessTree: true); } catch { }
                _proc = null;
            }

            string initCmd =
                "$PSStyle.OutputRendering = 'Ansi'; " +
                "$PSStyle.Formatting.Error = $PSStyle.Foreground.Red; " +
                "$PSStyle.Formatting.Warning = $PSStyle.Foreground.Yellow; " +
                "$PSStyle.Formatting.Verbose = $PSStyle.Foreground.Cyan; " +
                "$PSStyle.Formatting.Debug = $PSStyle.Foreground.Blue; " +
                "[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); " +
                "[Console]::InputEncoding  = [Text.UTF8Encoding]::new($false); " +
                "$OutputEncoding = [Console]::OutputEncoding";

            string arguments = string.IsNullOrEmpty(scriptPath)
                ? $"-NoLogo -NoExit -NoProfile -Command \"{initCmd}\""
                : $"-NoLogo -NoExit -NoProfile -Command \"{initCmd}; & '{scriptPath}' {args}\"";

            var psi = new ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding  = new UTF8Encoding(false),
                StandardInputEncoding  = new UTF8Encoding(false),
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                CreateNoWindow = true
            };

            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc.OutputDataReceived += (_, e) => AppendAnsi(e.Data);
            _proc.ErrorDataReceived  += (_, e) => AppendAnsi(e.Data);
            _proc.Exited += (_, __) => AppendAnsi("\n[Processus pwsh terminé]\n");

            try
            {
                if (!_proc.Start())
                {
                    Append("Impossible de démarrer pwsh. Vérifie l'installation de PowerShell 7.");
                    return;
                }

                _proc.BeginOutputReadLine();
                _proc.BeginErrorReadLine();

                Append(">> pwsh démarré. Ouvre un .ps1, F5 pour exécuter, F8 pour la sélection.");
            }
            catch (Exception ex)
            {
                Append($"[Erreur lancement pwsh] {ex.Message}");
            }
        }

        private static readonly Regex _sgr = new(@"\x1B\[(?<codes>[\d;]*)m", RegexOptions.Compiled);

        private void Append(string? text)
        {
            if (string.IsNullOrEmpty(text) || _closing) return;
            Dispatcher.Invoke(() =>
            {
                EnsureParagraph();
                var para = (Paragraph)Terminal.Document.Blocks.LastBlock;
                para.Inlines.Add(new Run(text));
                para.Inlines.Add(new LineBreak());
                Terminal.ScrollToEnd();
            });
        }

        private void AppendAnsi(string? text)
        {
            if (string.IsNullOrEmpty(text) || _closing) return;

            Dispatcher.Invoke(() =>
            {
                EnsureParagraph();
                var para = (Paragraph)Terminal.Document.Blocks.LastBlock;

                Brush currentBrush = Brushes.White;
                var currentWeight = FontWeights.Normal;
                var currentStyle  = FontStyles.Normal;

                int idx = 0;
                var matches = _sgr.Matches(text);
                
                if (matches.Count == 0)
                {
                    para.Inlines.Add(new Run(text) { Foreground = currentBrush });
                    para.Inlines.Add(new LineBreak());
                    Terminal.ScrollToEnd();
                    return;
                }
                
                foreach (Match m in matches)
                {
                    if (m.Index > idx)
                    {
                        para.Inlines.Add(new Run(text.Substring(idx, m.Index - idx))
                        {
                            Foreground = currentBrush,
                            FontWeight = currentWeight,
                            FontStyle  = currentStyle
                        });
                    }

                    var codes = m.Groups["codes"].Value.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    if (codes.Length == 0)
                    {
                        currentBrush = Brushes.White;
                        currentWeight = FontWeights.Normal;
                        currentStyle  = FontStyles.Normal;
                    }
                    else
                    {
                        foreach (var c in codes)
                        {
                            if (!int.TryParse(c, out var code)) continue;
                            switch (code)
                            {
                                case 0: currentBrush = Brushes.White; currentWeight = FontWeights.Normal; currentStyle = FontStyles.Normal; break;
                                case 1: currentWeight = FontWeights.Bold; break;
                                case 3: currentStyle  = FontStyles.Italic; break;
                                case 22: currentWeight = FontWeights.Normal; break;
                                case 23: currentStyle  = FontStyles.Normal; break;
                                case >= 30 and <= 37:
                                case >= 90 and <= 97:
                                    currentBrush = BrushFromCode(code); break;
                            }
                        }
                    }
                    idx = m.Index + m.Length;
                }

                if (idx < text.Length)
                {
                    para.Inlines.Add(new Run(text.Substring(idx))
                    {
                        Foreground = currentBrush,
                        FontWeight = currentWeight,
                        FontStyle  = currentStyle
                    });
                }

                para.Inlines.Add(new LineBreak());
                Terminal.ScrollToEnd();
            });
        }

        private static Brush BrushFromCode(int code) => code switch
        {
            30 => Brushes.Black,
            31 => (Brush)new BrushConverter().ConvertFromString("#E06C75")!,
            32 => (Brush)new BrushConverter().ConvertFromString("#98C379")!,
            33 => (Brush)new BrushConverter().ConvertFromString("#E5C07B")!,
            34 => (Brush)new BrushConverter().ConvertFromString("#61AFEF")!,
            35 => (Brush)new BrushConverter().ConvertFromString("#C678DD")!,
            36 => (Brush)new BrushConverter().ConvertFromString("#56B6C2")!,
            37 => Brushes.White,
            90 => Brushes.Gray,
            91 => Brushes.IndianRed,
            92 => Brushes.LightGreen,
            93 => Brushes.Khaki,
            94 => Brushes.LightSkyBlue,
            95 => Brushes.Plum,
            96 => Brushes.PaleTurquoise,
            97 => Brushes.White,
            _  => Brushes.White
        };

        private void EnsureParagraph()
        {
            if (Terminal.Document == null)
                Terminal.Document = new FlowDocument();
            if (Terminal.Document.Blocks.LastBlock is not Paragraph)
                Terminal.Document.Blocks.Add(new Paragraph());
            if (Terminal.Document.Blocks.Count == 0)
                Terminal.Document.Blocks.Add(new Paragraph());
        }

        #endregion

        #region Entrée utilisateur (prompt, historique, Enter)

        /// <summary>
        /// Gère Enter (envoi à pwsh) + historique ↑/↓ dans la zone d'entrée
        /// </summary>
        private async void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (_proc is null || _proc.HasExited) StartPwsh();

            if (e.Key == Key.Up)
            {
                if (_history.Count == 0) return;
                if (_historyIndex < 0) _historyIndex = _history.Count - 1;
                else if (_historyIndex > 0) _historyIndex--;
                Input.Text = _history[_historyIndex];
                Input.CaretIndex = Input.Text.Length;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down)
            {
                if (_history.Count == 0) return;
                if (_historyIndex >= 0 && _historyIndex < _history.Count - 1)
                {
                    _historyIndex++;
                    Input.Text = _history[_historyIndex];
                }
                else
                {
                    _historyIndex = -1;
                    Input.Clear();
                }
                Input.CaretIndex = Input.Text.Length;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                var line = Input.Text;
                Input.Clear();

                if (!string.IsNullOrWhiteSpace(line))
                {
                    _history.Add(line);
                    _historyIndex = -1;
                }

                Append($"> {line}");

                try
                {
                    await _proc!.StandardInput.WriteLineAsync(line);
                    await _proc.StandardInput.FlushAsync();
                }
                catch (Exception ex)
                {
                    Append($"[Erreur écriture stdin] {ex.Message}");
                }

                e.Handled = true;
            }
        }

        #endregion

        #region Éditeur : ouvrir/enregistrer/exécuter

        private void OpenMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void OpenScript_Click(object sender, RoutedEventArgs e) => OpenScript();
        private void OpenFromSharePoint_Click(object sender, RoutedEventArgs e) => OpenFromSharePoint();
        private void SaveScript_Click(object sender, RoutedEventArgs e) => SaveScript();
        private void RunFile_Click(object sender, RoutedEventArgs e) => RunFile();
        private void RunSelection_Click(object sender, RoutedEventArgs e) => RunSelection();

        private void OpenScript()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Scripts PowerShell (*.ps1)|*.ps1|Tous les fichiers (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                _currentPath = dlg.FileName;
                Editor.Text = File.ReadAllText(_currentPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                var version = GetApplicationVersion();
                Title = $"Powershell 7 ISE v{version} - {Path.GetFileName(_currentPath)}";
                Append($">> Script chargé: {_currentPath}");
                Input.Focus();
            }
        }

        private void OpenFromSharePoint()
        {
            var browser = new SharePointBrowser
            {
                Owner = this
            };

            if (browser.ShowDialog() == true && !string.IsNullOrEmpty(browser.SelectedFileContent))
            {
                _currentPath = null;
                Editor.Text = browser.SelectedFileContent;
                var version = GetApplicationVersion();
                Title = $"Powershell 7 ISE v{version} - {browser.SelectedFileName} (SharePoint)";
                Append($">> Script chargé depuis SharePoint: {browser.SelectedFileName}");
                Input.Focus();
            }
        }


        private void SaveScript()
        {
            if (string.IsNullOrEmpty(_currentPath))
            {
                var sfd = new SaveFileDialog { Filter = "Scripts PowerShell (*.ps1)|*.ps1" };
                if (sfd.ShowDialog() == true) _currentPath = sfd.FileName;
                else return;
            }
            File.WriteAllText(_currentPath!, Editor.Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Append($">> Script enregistré: {_currentPath}");
            Input.Focus();
        }

        /// <summary>Exécute le fichier courant</summary>
        private async void RunFile()
        {
            if (_proc is null || _proc.HasExited) StartPwsh();

            string path;
            if (!string.IsNullOrEmpty(_currentPath))
            {
                path = _currentPath;
            }
            else
            {
                path = Path.Combine(Path.GetTempPath(), "PsConsoleHost", "scratch.ps1");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, Editor.Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }

            try
            {
                await _proc!.StandardInput.WriteLineAsync($"& \"{path}\"");
                await _proc.StandardInput.FlushAsync();
            }
            catch (Exception ex) { Append($"[Erreur écriture stdin] {ex.Message}"); }

            Input.Focus();
        }

        /// <summary>Exécute la sélection (ou la ligne courante)</summary>
        private async void RunSelection()
        {
            if (_proc is null || _proc.HasExited) StartPwsh();

            string text = string.IsNullOrEmpty(Editor.SelectedText)
                ? GetCurrentLineText()
                : Editor.SelectedText;

            Append($">>> Exécution de {text.Split('\n').Length} ligne(s) (sélection)");
            try
            {
                using var sr = new StringReader(text);
                string? line;
                while ((line = sr.ReadLine()) != null)
                    await _proc!.StandardInput.WriteLineAsync(line);
                await _proc!.StandardInput.FlushAsync();
            }
            catch (Exception ex) { Append($"[Erreur écriture stdin] {ex.Message}"); }

            Input.Focus();
        }

        private string GetCurrentLineText()
        {
            var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
            return Editor.Document.GetText(line);
        }

        #endregion

        #region Arrêter / Clear

        /// <summary>
        /// Arrête le processus pwsh en cours et redémarre une nouvelle session
        /// </summary>
        private async void SendCtrlC_Click(object sender, RoutedEventArgs e)
        {
            if (_proc is { HasExited: false })
            {
                try { _proc.Kill(entireProcessTree: true); } catch { }
                await Task.Delay(250);
                StartPwsh();
                Input.Focus();
            }
        }

        private void Clear_Click(object? sender, RoutedEventArgs? e)
        {
            Terminal.Document.Blocks.Clear();
            Input.Focus();
        }

        /// <summary>
        /// Vérifie manuellement les mises à jour disponibles
        /// </summary>
        private void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            UpdateService.CheckForUpdate();
            Input.Focus();
        }

        /// <summary>
        /// Affiche la boîte de dialogue "À propos"
        /// </summary>
        private void About_Click(object sender, RoutedEventArgs e)
        {
            var version = GetApplicationVersion();
            var message = $"Powershell 7 ISE\n\n" +
                         $"Version {version}\n\n" +
                         $"Environnement de développement intégré pour PowerShell 7\n\n" +
                         $"© 2025 Powershell 7 ISE";
            
            MessageBox.Show(
                message,
                "À propos de Powershell 7 ISE",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// Met à jour le titre de la fenêtre avec la version
        /// </summary>
        private void UpdateWindowTitle()
        {
            var version = GetApplicationVersion();
            Title = $"Powershell 7 ISE v{version}";
        }

        /// <summary>
        /// Récupère la version de l'application depuis l'assembly
        /// </summary>
        private string GetApplicationVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                if (version != null)
                {
                    if (version.Build >= 0)
                    {
                        return $"{version.Major}.{version.Minor}.{version.Build}";
                    }
                    else
                    {
                        return $"{version.Major}.{version.Minor}";
                    }
                }
                
                // Essayer avec AssemblyInformationalVersion
                var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (infoVersion != null && !string.IsNullOrEmpty(infoVersion.InformationalVersion))
                {
                    var versionStr = infoVersion.InformationalVersion.Split('+')[0].Split('-')[0];
                    if (System.Version.TryParse(versionStr, out var parsedVersion))
                    {
                        return parsedVersion.Build >= 0 
                            ? $"{parsedVersion.Major}.{parsedVersion.Minor}.{parsedVersion.Build}"
                            : $"{parsedVersion.Major}.{parsedVersion.Minor}";
                    }
                }
            }
            catch
            {
                // En cas d'erreur, retourner une version par défaut
            }
            return "1.0.0";
        }

        #endregion
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Wox.Plugin;

using ProjectLauncher.Core.IPC;

namespace Community.PowerToys.Run.Plugin.ProjectLauncher
{
    public class Main : IPlugin, IContextMenu
    {
        public static string PluginID => "EAC611DB-B5BB-4467-BD5C-F870F95BEDBC";
        private PluginInitContext? _context;
        private ProjectLauncherClient? _client;
        private Dictionary<string, string>? _ideKeywords;

        public string Name => "Project Launcher";
        public string Description => "Search projects from Project Launcher";

        public void Init(PluginInitContext context)
        {
            try 
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "project_launcher_plugin_debug.txt"), "Starting Init...\n");
                _context = context;
                _client = new ProjectLauncherClient();
                
                // Fetch IDE keywords from server
                Task.Run(async () => {
                    try {
                        _ideKeywords = await _client.GetSettingsAsync();
                        System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "project_launcher_plugin_debug.txt"), $"Loaded {_ideKeywords?.Count ?? 0} IDE keywords.\n");
                    } catch (Exception ex) {
                        System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "project_launcher_plugin_debug.txt"), $"Failed to load IDE keywords: {ex.Message}\n");
                    }
                });
                
                System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "project_launcher_plugin_debug.txt"), "Init success.\n");
            }
            catch (Exception ex)
            {
                 System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "project_launcher_plugin_debug.txt"), $"Init Failed: {ex}\n");
                 throw;
            }
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            if (selectedResult.ContextData is not IpcProjectResult p)
                return new List<ContextMenuResult>();

            var menus = new List<ContextMenuResult>();

            // Open in Explorer
            menus.Add(new ContextMenuResult
            {
                PluginName = Name,
                Title = "Open in Explorer",
                Glyph = "\uE838", // FolderOpen
                FontFamily = "Segoe MDL2 Assets",
                Action = _ =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{p.Path}\"");
                        return true;
                    }
                    catch { return false; }
                }
            });

            // Open Terminal
            menus.Add(new ContextMenuResult
            {
                PluginName = Name,
                Title = "Open Terminal",
                Glyph = "\uE756", // CommandPrompt
                FontFamily = "Segoe MDL2 Assets",
                Action = _ =>
                {
                    try
                    {
                        // Try Windows Terminal first, fall back to cmd
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "wt",
                            Arguments = $"-d \"{p.Path}\"",
                            UseShellExecute = true
                        });
                        return true;
                    }
                    catch 
                    {
                        try 
                        {
                             System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "cmd",
                                Arguments = $"/k cd /d \"{p.Path}\"",
                                UseShellExecute = true
                            });
                            return true;
                        } catch { return false; }
                    }
                }
            });

            // Open Admin Terminal
            menus.Add(new ContextMenuResult
            {
                PluginName = Name,
                Title = "Open Terminal (Admin)",
                Glyph = "\uE7EF", // Admin
                FontFamily = "Segoe MDL2 Assets",
                Action = _ =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "wt",
                            Arguments = $"-d \"{p.Path}\"",
                            Verb = "runas",
                            UseShellExecute = true
                        });
                        return true;
                    }
                    catch 
                    {
                         try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "cmd",
                                Arguments = $"/k cd /d \"{p.Path}\"",
                                Verb = "runas",
                                UseShellExecute = true
                            });
                            return true;
                        } catch { return false; }
                    }
                }
            });
            
            // Open Repository
            if (!string.IsNullOrEmpty(p.GitUrl))
            {
                menus.Add(new ContextMenuResult
                {
                    PluginName = Name,
                    Title = "Open Repository",
                    Glyph = "\uE774", // Globe
                    FontFamily = "Segoe MDL2 Assets",
                    Action = _ =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = p.GitUrl,
                                UseShellExecute = true
                            });
                            return true;
                        }
                        catch { return false; }
                    }
                });
            }

            return menus;
        }

        private static readonly string[] KnownIdeKeywords = new[] { "vscode", "code", "cursor", "windsurf", "visualstudio", "vs", "rider", "intellij", "idea", "webstorm", "pycharm", "clion", "goland", "phpstorm", "sublime", "atom", "nebula" };

        public List<Result> Query(Query query)
        {
            if (string.IsNullOrEmpty(query.Search))
            {
                return new List<Result>
                {
                    new Result 
                    {
                        Title = "Type to search projects...",
                        SubTitle = "Example: pl my-project | pl new vscode my-project",
                        IcoPath = "Images\\icon.png",
                        Action = _ => true
                    }
                };
            }

            // Handle "new" command
            if (query.Search.StartsWith("new ", StringComparison.OrdinalIgnoreCase))
            {
                var remainder = query.Search.Substring(4).Trim(); 
                // Parsing: <ide> <project>
                // Try to find if first word is an IDE
                var parts = remainder.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                string targetIde = "";
                string targetProject = "";
                
                if (parts.Length == 2)
                {
                    var potentialIde = parts[0].ToLowerInvariant();
                    
                    // Check hardcoded keywords first
                    if (KnownIdeKeywords.Contains(potentialIde))
                    {
                        targetIde = potentialIde;
                        targetProject = parts[1];
                    }
                    // Then check user-configured IDE keywords from server
                    else if (_ideKeywords != null && _ideKeywords.TryGetValue(potentialIde, out var mappedIde))
                    {
                        targetIde = mappedIde; // Use the mapped IDE id (e.g., "gv" -> "antigravity")
                        targetProject = parts[1];
                    }
                    else
                    {
                        targetProject = remainder; // No specific IDE found, treat all as project name
                    }
                }
                else
                {
                    targetProject = remainder;
                }

                if (string.IsNullOrEmpty(targetProject))
                {
                     return new List<Result>
                    {
                        new Result 
                        {
                            Title = "New Project: <ide> <name>",
                            SubTitle = "e.g. 'pl new macchu-picchu' or 'pl new vscode macchu-picchu'",
                            IcoPath = "Images\\icon.png",
                            Action = _ => true
                        }
                    };
                }

                return new List<Result>
                {
                    new Result 
                    {
                        Title = $"Create project '{targetProject}' {(string.IsNullOrEmpty(targetIde) ? "" : $"in {targetIde}")}",
                        SubTitle = "Click to create and open",
                        IcoPath = "Images\\icon.png",
                        Action = _ => 
                        {
                            try 
                            { 
                                if (_client == null) return false;
                                
                                var req = new IpcSearchRequest
                                {
                                    Type = IpcRequestType.Create,
                                    TargetIde = targetIde,
                                    TargetProject = targetProject
                                };
                                
                                // Fire and forget mostly, or wait for response?
                                // Wox Query is sync, Action is sync bool.
                                // We can run async task.
                                Task.Run(async () => {
                                    try {
                                        var response = await _client.SendRequestAsync(req);
                                        if (!response.Success) _context?.API.ShowMsg($"Creation Failed: {response.Message}");
                                    } catch(Exception ex) {
                                      _context?.API.ShowMsg($"Creation Error: {ex.Message}");
                                    }
                                });
                                
                                return true;
                            } 
                            catch { return false; }
                        }
                    }
                };
            }

            // Handle "IDE Launch" command (pl <ide> <project>)
            // Check if start with IDE keyword
            string? forcedIde = null;
            
            // We do NOT strip the query anymore, to allow Server-side SearchEngine to apply filtering logic.
            // But we detect the forcedIde here just for the Action override if needed, 
            // though ideally Server returns the correct specific IDE result now.
            
            var queryParts = query.Search.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (queryParts.Length >= 1)
            {
                var potentialIde = queryParts[0].ToLowerInvariant();
                if (KnownIdeKeywords.Contains(potentialIde))
                {
                    forcedIde = potentialIde;
                }
                // Also check user-configured IDE keywords
                else if (_ideKeywords != null && _ideKeywords.TryGetValue(potentialIde, out var mappedIde))
                {
                    forcedIde = mappedIde;
                }
            }
            
            string effectiveQuery = query.Search;

            try 
            {
                if (_client == null) return new List<Result>();
                
                // Normal search
                var searchResults = Task.Run(() => _client.QueryAsync(effectiveQuery)).Result; 
                
                return searchResults.Select(r => new Result
                {
                    Title = r.Name,
                    SubTitle = $"{r.IdeName} • {string.Join(", ", r.Languages)} • {r.Path}",
                    IcoPath = r.IdeIconPath,
                    Score = r.Score,
                    Action = _ =>
                    {
                        try 
                        {
                            // Send Launch command to the main app via IPC
                            // The app handles all the IDE launching properly without terminal windows
                            Task.Run(async () => {
                                try {
                                    var response = await _client.LaunchAsync(r.Path, r.IdeId);
                                    if (!response.Success) 
                                    {
                                        _context?.API.ShowMsg($"Launch failed: {response.Message}");
                                    }
                                } catch (Exception ex) {
                                    _context?.API.ShowMsg($"Launch error: {ex.Message}");
                                }
                            });
                            return true;
                        }
                        catch (Exception ex)
                        {
                            _context?.API.ShowMsg($"Error opening {r.Name}: {ex.Message}");
                            return false;
                        }
                    },
                    ContextData = r 
                }).ToList();
            }
            catch (Exception ex)
            {
               return new List<Result> { 
                   new Result { 
                       Title = "Error querying Project Launcher", 
                       SubTitle = ex.Message,
                       IcoPath = "Images\\app.png"
                   }
               };
            }
        }
    }
}


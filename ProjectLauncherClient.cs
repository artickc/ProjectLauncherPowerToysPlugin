using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Tasks;
using ProjectLauncher.Core.IPC;

namespace Community.PowerToys.Run.Plugin.ProjectLauncher
{
    public class ProjectLauncherClient
    {
        private const string PipeName = "AtC.ProjectLauncher.IPC";

        public async Task<List<IpcProjectResult>> QueryAsync(string query)
        {
            var req = new IpcSearchRequest
            {
                Type = IpcRequestType.Search,
                Query = query,
                Limit = 20
            };
            
            var response = await SendRequestAsync(req);
            return response.Results ?? new List<IpcProjectResult>();
        }

        public async Task<Dictionary<string, string>?> GetSettingsAsync()
        {
            var req = new IpcSearchRequest
            {
                Type = IpcRequestType.GetSettings
            };
            
            var response = await SendRequestAsync(req);
            return response.IDEKeywords;
        }

        public async Task<IpcSearchResponse> LaunchAsync(string projectPath, string ideId)
        {
            var req = new IpcSearchRequest
            {
                Type = IpcRequestType.Launch,
                TargetProject = projectPath,
                TargetIde = ideId
            };
            
            return await SendRequestAsync(req);
        }

        public async Task<IpcSearchResponse> SendRequestAsync(IpcSearchRequest request)
        {
             try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
                var cts = new System.Threading.CancellationTokenSource(500); // 500ms timeout
                
                try 
                {
                    await client.ConnectAsync(cts.Token);
                }
                catch
                {
                    return new IpcSearchResponse { Success = false, Message = "Connection failed" };
                }

                using var reader = new StreamReader(client);
                using var writer = new StreamWriter(client) { AutoFlush = true };

                var jsonRequest = JsonSerializer.Serialize(request);
                await writer.WriteLineAsync(jsonRequest);

                var jsonResponse = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(jsonResponse)) return new IpcSearchResponse { Success = false, Message = "Empty response" };

                var response = JsonSerializer.Deserialize<IpcSearchResponse>(jsonResponse);
                return response ?? new IpcSearchResponse { Success = false, Message = "Invalid response" };
            }
            catch (Exception ex)
            {
                return new IpcSearchResponse { Success = false, Message = ex.Message };
            }
        }
    }
}

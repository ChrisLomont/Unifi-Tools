using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lomont.UnifiTools
{
    public class Unifi
    {
        State state;

        public List<Camera> Cameras { get; } = new();

        public Unifi(string login, string password, Log.LevelType logLevel = Log.LevelType.Info)
        {
            state = new State(login,password)
            {
                Log =
                {
                    Level = logLevel
                }
            };
        }

        // after login and cameras, this true
        bool initialized = false;

        public async Task Init()
        {
            // login, get cameras
            await Login(state);
            await GetCameras();

        }

        // make a filename based on a camera name and a start and end time
        // if no endtime, make a jpeg
        // if endtime, make a mp4
        string MakeFilename(string cameraName, DateTime startTime, DateTime? endTime = null, bool sequential = false)
        {

            var ext = endTime == null ? ".jpg" : ".mp4";
            var baseName = cameraName.Replace(" ", "_");
            string filename;
            if (sequential)
            {
                filename = $"{baseName}_{sequentialCapture:000000}{ext}";
                ++sequentialCapture;
            }
            else
            {
                var startText = FormatDt(startTime);
                var endText = FormatDt(endTime);
                filename = $"{baseName}{startText}{endText}{ext}";
            }

            return filename;

            string FormatDt(DateTime? dt)
            {
                if (dt == null)
                    return "";
                var v = dt.Value;
                return "__" + v.ToString("yyyy_MM_dd-hh_mm_ss_fff");
            }
        }



        public async Task ProcessJob(CaptureJob job)
        {
            sequentialCapture = 0; // reset counter
            var dt = (job.EndTime - job.StartTime).TotalSeconds;
            var frames = job.Fps * job.RuntimeSeconds;
            var del = TimeSpan.FromSeconds(dt / frames); // delta per frame
            var time = job.StartTime;
            var foreSave = Console.ForegroundColor;
            for (var i = 0; i < frames; ++i)
            {
                await GetSnapshot(job.CameraName, time, sequential: true);
                time += del;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"{i + 1}/{frames} files");
                Console.ForegroundColor = foreSave;
            }

            // convert to video
            // ffmpeg to video
            // https://shotstack.io/learn/use-ffmpeg-to-convert-images-to-video/
            // ffmpeg -framerate 30 -i happy%d.jpg -c:v libx264 -r 30 -pix_fmt yuv420p output.mp4
            // if frames sorted, but not with nice index, replace happy%d.jpg with -pattern_type glob -i '*.jpg
            // can add audio: - todo - use ffmpeg to aplit audio out of a larger capture
            //    add -i freeflow.mp3 -shortest  after jpg input specifier, adds mp3 input, uses shortest file as length

            // 
            // ffmpeg -framerate 30 -i Camera_-_Office_window_%06d.jpg -c:v libx264 -r 30 -pix_fmt yuv420p output.mp4
            // ffmpeg -framerate 1 -pattern_type glob -i '*.jpg' -c:v libx264 -r 30 -pix_fmt yuv420p output.mp4
            // ffmpeg -framerate 30 -i Camera_-_Office_window_%06d.jpg -c:v libx264 -r 30 -pix_fmt yuv420p output.mp4

            var filePattern = MakeFilename(job.CameraName, job.StartTime, sequential: true);
            filePattern = filePattern.Substring(0, filePattern.Length - "000000.jpg".Length);

            var commandArgs = $"-framerate {job.Fps} -i {filePattern}%06d.jpg -c:v libx264 -r {job.Fps} -pix_fmt yuv420p {job.OutputFilename}";

            RunProcess("ffmpeg.exe", commandArgs);
        }


        // convert date time into POSIX timestamp (which is seconds)
        // has to conver to local UTC to get correct time from API
        // then into milliseconds for Unifi API
        static long MakeTimestampMs(DateTime time)
        {
            // correct for UTC
            var dt1 = DateTime.Now;
            var dt2 = DateTime.UtcNow;
            var offs = dt2 - dt1;
            time += offs;

            // POSIX timestamp
            var unixTimestamp = (long)time.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            return unixTimestamp * 1000;
        }

        Camera LookupCamera(string cameraName)
        {
            return Cameras.First(c => c.Name.Trim() == cameraName);
        }


        public async Task GetVideo(string cameraName, DateTime startTime, DateTime endTime, bool sequential = false)
        {
            var camera = LookupCamera(cameraName);

            // todo - put into 1 hour chunks, loop over it, replace missing files...
            var startTimestamp = MakeTimestampMs(startTime);
            var endTimestamp = MakeTimestampMs(endTime);
            var videoQuery = $"/video/export?camera={camera.Id}&start={startTimestamp}&end={endTimestamp}";
            var filename = MakeFilename(camera.Name, startTime, endTime, sequential: sequential);
            await DownloadFile(state, filename, videoQuery);
        }

        void RunProcess(string exeName, string commandArgs)
        {
            var p = new Process();
            p.StartInfo.FileName = exeName;
            p.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
            p.StartInfo.Arguments = commandArgs;
            p.Start();
            p.WaitForExit();
        }


        // does not work yet - not sure how - needs reversed
        public async Task GetSnapshot(string cameraName, DateTime time, bool sequential = false)
        {
            var camera = LookupCamera(cameraName);
#if true
            // pull a video
            var endTime = time + TimeSpan.FromSeconds(1);
            await GetVideo(cameraName, time, endTime, sequential: false);

            var videoName = MakeFilename(camera.Name, time, endTime);
            var imageName = MakeFilename(camera.Name, time, sequential: sequential);
            var frameNumber = 0; // 0 indexed frame number
            var commandArgs = $"-i {videoName} -vf \"select=eq(n\\,{frameNumber})\" -vframes 1 {imageName}";

            RunProcess("ffmpeg.exe", commandArgs);

            File.Delete(videoName);

#else
    // this method does not yet work
    var timestamp = MakeTimestampMs(time);
    //# build snapshot export API address
    //    snapshot_export_query = f"/cameras/{camera.id}/snapshot?ts={js_timestamp_start}"

    // NOTE: from online comments, this does not work  - recommended using short video snippets and pulling a frame

    var query = $"/cameras/{camera.Id}/snapshot?ts={timestamp}";

    var filename = MakeFilename(camera.Name, time);

    await DownloadFile(state, filename, query);
#endif
        }



        // download a file from Unifi using the given filename using the given query URL
        static async Task DownloadFile(State state, string filename, string query)
        {
            var url = $"{state.Authority}{state.BasePath}{query}";

            state.Log.Info($"Downloading file {filename}");

            var response = await Get(state, url, authenticate: true);
            state.Log.Info($"Content length {response.Content.Headers.ContentLength}");

            // todo - show progress, do multiple in parallel
            await using Stream streamToReadFrom = await response.Content.ReadAsStreamAsync();
            await using Stream streamToWriteTo = File.Open(filename, FileMode.Create);
            await streamToReadFrom.CopyToAsync(streamToWriteTo);
        }


        static string ResponseToText(Log log, HttpResponseMessage response)
        {
            using var dataStream = response.Content.ReadAsStream();
            // Open the stream using a StreamReader for easy access.
            using var reader = new StreamReader(dataStream);
            // Read the content.
            var responseText = reader.ReadToEnd();
            // Display the content.
            log.Verbose($"Response: {responseText}");
            return responseText;
        }


        // get cameras, fills in state
        async Task GetCameras()
        {
            var url = $"{state.Authority}{state.BasePath}/cameras";
            var response = await Get(state, url, authenticate: true);

            var responseText = ResponseToText(state.Log, response);

            var options = new JsonDocumentOptions { AllowTrailingCommas = true };
            using var jsonDoc = JsonDocument.Parse(responseText, options);

            foreach (var elt in jsonDoc.RootElement.EnumerateArray())
            {
                //var items = new [] { "name", "type","id", "mac", "host"};
                //Console.WriteLine("\n\n\n******************************************\n");
                ////Console.WriteLine($"Elt => {elt}");
                //foreach (var item in items)
                //{
                //    Console.WriteLine("   {0,8}: {1}", item,elt.GetProperty(item));
                //}

                Cameras.Add(new Camera
                {
                    Name = elt.GetProperty("name").ToString(),
                    Host = elt.GetProperty("host").ToString(),
                    Mac = elt.GetProperty("mac").ToString(),
                    Type = elt.GetProperty("type").ToString(),
                    Id = elt.GetProperty("id").ToString()
                });
            }
        }


        static async Task<HttpResponseMessage> Get(State state, string url, string json = "", bool authenticate = false)
        {
            HttpResponseMessage response;
            if (json != "")
            {
                var data = new StringContent(json, Encoding.UTF8, "application/json");
                response = await state.Client.PostAsync(url, data);
            }
            else if (authenticate)
            {
                var message = new HttpRequestMessage(HttpMethod.Get, url);
                message.Headers.Add("Cookie", $"TOKEN={state.ApiToken}");
                response = await state.Client.SendAsync(message);
            }
            else
            {
                throw new Exception("Unfinished Get style");
            }

            // Display the status.
            state.Log.Verbose($"Response code: {response.StatusCode}");
            return response;
        }



        // filename index
        int sequentialCapture = 0;

        // login, fills in state
        static async Task Login(State state)
        {
            var url = $"{state.Authority}/api/auth/login";
            var json = $"{{\"username\":\"{state.Login}\", \"password\" : \"{state.Password}\" }}";
            var response = await Get(state, url, json: json);

            state.Log.Verbose("Headers: ");
            foreach (var header in response.Headers)
                state.Log.Verbose($"   {header.Key} => {header.Value}");

            if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies))
                throw new Exception("Cannot get headers");

            var authText = setCookies.FirstOrDefault() ?? "";

            state.Log.Verbose($"API Token: {authText}");

            // split out token part:
            var reg = new Regex($"TOKEN=([^;]+);");
            authText = reg.Match(authText).Groups[1].Value;
            state.Log.Verbose($"API Token: {authText}");

            // save it for using the API
            state.ApiToken = authText;
        }



        // store a camera object
        public class Camera
        {
            public string Name { get; set; }
            public string Mac { get; set; }
            public string Type { get; set; }
            public string Host { get; set; }
            public string Id { get; set; }
        }

        // simple console logger
        public class Log
        {
            public enum LevelType
            {
                Verbose,
                Info,
                Warn,
                Error,
            };

            public LevelType Level = LevelType.Verbose;

            public void Verbose(string message)
            {
                if (Level <= LevelType.Verbose)
                    Console.WriteLine(message);
            }
            public void Info(string message)
            {
                if (Level <= LevelType.Info)
                    Console.WriteLine(message);
            }
            public void Warn(string message)
            {
                if (Level <= LevelType.Warn)
                    Console.WriteLine(message);
            }
            public void Error(string message)
            {
                if (Level <= LevelType.Error)
                    Console.WriteLine(message);
            }

        }

        // track state of the API connection
        class State
        {
            public State(string login, string password)
            {
                this.Login = login;
                this.Password = password;
                ApiToken = "";
                Authority = $"{Protocol}://{Address}:{Port}";

                Handler = new(); // todo - needs IDisposable
                Handler.ServerCertificateCustomValidationCallback =
                    (message, cert, chain, errors) =>
                    { // todo - dangerous, only run on things you trust
                Console.WriteLine("SSL Cert processed");
                        return true;
                    };
                // https://d-fens.ch/2016/12/27/howto-set-cookie-header-on-defaultrequestheaders-of-httpclient/
                Handler.UseCookies = false; // needed to use our custom cookies? 
                Client = new HttpClient(Handler); // todo -needs IDisposable
            }

            public Log Log { get; } = new();

            HttpClientHandler Handler { get; }
            public HttpClient Client { get; }

            public string Login { get; }
            public string Password { get; }
            public string Protocol = "https";
            public string Address = "unifi";
            public int Port = 443;
            public string Authority;
            public string BasePath = "/proxy/protect/api";
            public string ApiToken { get; set; } // obtained from login
        }

        public record CaptureJob(string CameraName, DateTime StartTime, DateTime EndTime, int RuntimeSeconds, int Fps, string OutputFilename);

    }
}

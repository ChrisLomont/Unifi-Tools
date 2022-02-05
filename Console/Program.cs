// Console front end for playing with UnifiTools
// Chris Lomont 2022
// initial goal - pull video, make timelapses, works

/* TODO
 *   1. make command line tool
 *   2. pass login, pwd
 *   3. list cameras (names, index)
 *   4. download large video ranges in blocks
 *   5. download video snippets
 *   6. ffmpeg to do some work?
 *   7. Refactor, cleanup
 *   8. better messaging, errors
 *   9. redo missing or failed files
 *  10. make State IDisposable to clear HttpClient
 *  11. hide ffmpeg unless verbose. show file counter
 *  12. ffmpeg to video at end, delete images?
 *  13. 
 */

using Lomont.UnifiTools;



//var argCount = args.Length;
//if (argCount == 0)
//{
//    // Usage
//    Console.WriteLine("Usage:");
//    Console.WriteLine("   -l login");
//    Console.WriteLine("   -p password");
//    Environment.Exit(-1);
//}
// Console.WriteLine($"arg count {argCount}");

/* Wants:
 Camera - Office Window, Feb 1, 9 pm to Feb 4 1200 noon
 */

/* Command line
 * util.exe login password <list of things>
 * no things: dumps cameras
 * things:
 *    filename - script of things - read file, one per line
 * else -j job, -s snapshot, -v video
 * snapshot: camera name, start time, [outname.jpg]
 * video: camera name, start time, end time, [outname.mp4]
 * job: camera name, start time, end time, runtime (s), fps, [outname.mp4]
 * datetime: 2022-03-01-13:45:30 (24 hour format)
 */

//if (args.Length < 2)
//{
//    Console.WriteLine("Usage:");
//    Console.WriteLine("exename <login password <things>>");
//    Console.WriteLine("      login & password for Unifi Protect, in quotes if needed for spaces");
//    Console.WriteLine("      If no <things>, show a list of camera names.");
//    Console.WriteLine("      If only a filename passed in, first line is login, second password, each following line in the file is a thing to do.");
//    Console.WriteLine("      A thing is snapshot, a video, or a timelapse, as follows");
//    Console.WriteLine("   Snapshot  : -s cameraName time [outname.jpg]");
//    Console.WriteLine("   Video     : -v cameraName startTime endTime [outname.mp4]");
//    Console.WriteLine("   Timelapse : -t cameraName startTime endTime runtimeSeconds framesPerSecond [outname.mp4]");
//    Console.WriteLine();
//    Console.WriteLine("   output filenames are optional, have date/time as name by default");
//    Console.WriteLine("   times are of form: 2022-03-01T13:45:30  in 24 hour format");
//  //  Environment.Exit(-1);
//}

//var (login,password) = (args[0],args[1]);


#if false
// some sample jobs and such

var fps = 60; // frames per second timelapse
var runtimeSeconds = 20; // number of seconds in timelapse (3 of them)

var start1 = new DateTime(2022, 02, 02, 5, 0, 0);
var end1 = new DateTime(2022, 02, 04, 4+12, 0, 0);

var job1 = new Unifi.CaptureJob(
    CameraName: "Camera - Office window",
    StartTime: start1,
    EndTime: end1,
    RuntimeSeconds:runtimeSeconds,
    Fps:fps,
    OutputFilename:"Office.mp4"
);

var job2 = new Unifi.CaptureJob(
    CameraName: "Camera - Kitchen door",
    StartTime: start1,
    EndTime: end1,
    RuntimeSeconds: runtimeSeconds,
    Fps: fps,
    OutputFilename: "Kitchen.mp4"
);
var job3 = new Unifi.CaptureJob(
    CameraName: "Camera - Front door",
    StartTime: start1,
    EndTime: end1,
    RuntimeSeconds: runtimeSeconds,
    Fps: fps,
    OutputFilename: "FrontDoor.mp4"
);

// testing
var job4 = new Unifi.CaptureJob(
    CameraName: "Camera - Office window",
    StartTime: start1,
    EndTime: end1,
    RuntimeSeconds: 10,
    Fps: 20,
    OutputFilename: "OfficeTest.mp4"
);
#endif

// insert your name and password here, edit rest as you see fit
var unifi = new Unifi(login:todo, password:todo, Unifi.Log.LevelType.Info);

// login, get cameras
await unifi.Init();

// camera names
foreach (var c in unifi.Cameras)
    Console.WriteLine($"Cam: [{c.Name}]");


// how to process jobs
//await unifi.ProcessJob(job1);
//await unifi.ProcessJob(job2);
//await unifi.ProcessJob(job3);
//await unifi.ProcessJob(job4);

// how to grab a snapshot 
//var cameraName = "Camera - Kitchen door";
//var startTime = new DateTime(2022, 2, 2, 13, 35, 45);
//await unifi.GetSnapshot(cameraName, startTime);


// how to grab video
//var endTime = startTime + TimeSpan.FromSeconds(2); // gets about 5 seconds
//await unifi.GetVideo(cameraName, startTime, endTime);

// end of file

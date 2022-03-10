using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Collections;
using System.Data.SqlClient;
using System.Data;
using System.IO;
using System.Threading;
using System.Timers;
using System.Net.Mail;

namespace StoresDBCheckService
{
    class Service
    {
       
        public static string PhotoWatchFolder,AlbumWatchFolder, TimerEventCopyPasteSources, TimerEventCopyPasteDestinations;
        public static string OrderDetailsFolder,PhotoPrintFolderPath,AlbumPrintFolderPath,LogPath, ArchivePath;
        private static FileSystemWatcher Photowatcher, Albumewatcher;
        public static string FileNames, ArchiveStatusCodes,AlbumCode = "1", PhotoCode = "2";
        public static int FilesCount, ArchiveLimitDays, TimeIntervalMin;
        public static System.Timers.Timer timer;

        private static List<string> CopiedFiles = new List<string>();
        public static string SQLConnectionString;
        public static bool ChangeDay = true;
        //= "Data Source=jliq-sql012A,51001;Initial Catalog = StoresCheckService; User ID = it3testuser; Password = Shell@123;";
        public Service()
        {
            Log("service Started");
            ReadConfigSettings();
            int timeout = 60000 * TimeIntervalMin;
            timer = new System.Timers.Timer(timeout);
            timer.Elapsed += TimerElapsed;
            timer.AutoReset = true;
            timer.Enabled = true;
            
        }

        private static void Email(string msg)
        {
            string to = ConfigurationManager.AppSettings["AlertToEmail"]; //To address    
            string from = ConfigurationManager.AppSettings["EmailId"]; //From address
            string pass = ConfigurationManager.AppSettings["Password"];
            MailMessage message = new MailMessage(from, to);

            string mailbody = msg;
            message.Subject = "Error in Laharika Service Application";
            message.Body = mailbody;
            message.BodyEncoding = Encoding.UTF8;
            message.IsBodyHtml = true;
            SmtpClient client = new SmtpClient("smtp.gmail.com", 587); //Gmail smtp    
            System.Net.NetworkCredential basicCredential1 = new
            System.Net.NetworkCredential(from, pass);
            client.EnableSsl = true;
            client.UseDefaultCredentials = false;
            client.Credentials = basicCredential1;
            try
            {
                client.Send(message);
            }

            catch (Exception ex)
            {
                Log("Error Sending Email : " + ex.Message);
                //throw ex;
            }
        }
        private static void Setup()
        {
            ReadConfigSettings();
            InitializePhotoWatcher();
            InitializeAlbumWatcher();
            //Email("test mail");
        }

        public void Start()
        {
            Log("service Started");
            ReadConfigSettings();
            Setup();
        }

        protected static void ReadConfigSettings()
        {
            SQLConnectionString = ConfigurationManager.ConnectionStrings["ConnectionString"].ToString();
            PhotoWatchFolder = ConfigurationManager.AppSettings["PhotoWatchFolder"].ToString();
            AlbumWatchFolder = ConfigurationManager.AppSettings["AlbumWatchFolder"].ToString();
            OrderDetailsFolder = ConfigurationManager.AppSettings["OrderDetailsFolder"].ToString();
            PhotoPrintFolderPath = ConfigurationManager.AppSettings["PhotoPrintFolderPath"].ToString();
            AlbumPrintFolderPath = ConfigurationManager.AppSettings["AlbumPrintFolderPath"].ToString();
            TimerEventCopyPasteSources = ConfigurationManager.AppSettings["TimerEventCopyPasteSources"].ToString();
            TimerEventCopyPasteDestinations = ConfigurationManager.AppSettings["TimerEventCopyPasteDestinations"].ToString();
            ArchiveStatusCodes = ConfigurationManager.AppSettings["ArchiveStatusCodes"];
            ArchivePath = ConfigurationManager.AppSettings["ArchivePath"];
            ArchiveLimitDays = Convert.ToInt32(ConfigurationManager.AppSettings["ArchiveLimitDays"]);
            TimeIntervalMin = Convert.ToInt32(ConfigurationManager.AppSettings["TimeIntervalMin"]);
            LogPath = ConfigurationManager.AppSettings["LogPath"];
        }

        private static void TimerElapsed(Object source, ElapsedEventArgs e)
        {
            int runat = Convert.ToInt32(ConfigurationManager.AppSettings["TimerElapsedTimein24Hr"]);
            if (Convert.ToInt32(DateTime.Now.ToString("HH")) == runat)
            {
                Log("Timer elapsed, time for cleaning");
                if (TimerEventCopyPasteSources.Length > 3)
                {
                    string[] sources = TimerEventCopyPasteSources.Split(',');
                    string[] destinations = TimerEventCopyPasteDestinations.Split(',');
                    if (sources.Length != destinations.Length)
                    {
                        return;
                    }

                    for (int i = 0; i < sources.Length; i++)
                    {
                        CopyFilesRecursively(sources[i], destinations[i]);
                        VerfiyAndRemove(sources[i], destinations[i]);
                    }
                }

                cleanFolders(AlbumCode,AlbumPrintFolderPath);
                cleanFolders(PhotoCode,PhotoPrintFolderPath);
                CleanArchive();
            }
        }

        private static void CleanArchive()
        {
            Log("Cleaning Archive");
            string sourcePath = ArchivePath;
            foreach (string path in Directory.GetDirectories(sourcePath))
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(path);
                var orderdate = directoryInfo.CreationTime;

                if ((DateTime.Now - orderdate).Days > ArchiveLimitDays)
                {
                    Log("Deleting File name : " + path);
                    Directory.Delete(path, true);
                }
            }
        }
        
        private static void cleanFolders(string code, string sourcePath)
        {
            Log("clean folder with code "+ code + " source path : " + sourcePath);
            List<string> checkcodes = new List<string>();
            foreach (string codename in ArchiveStatusCodes.Split(','))
            {
                checkcodes.Add("_" + codename);
            }
            var orders = Directory.GetFiles(OrderDetailsFolder, code+"*");
            foreach(var order in orders)
            {
                if (checkcodes.Contains(Path.GetFileNameWithoutExtension(order).Split('$')[1]))
                {
                     if(Directory.Exists(Path.Combine(sourcePath, Path.GetFileNameWithoutExtension(order).Split('$')[0])))
                    {
                        Log("Moving file from " + Path.Combine(sourcePath, Path.GetFileNameWithoutExtension(order).Split('$')[0]) + " Destination : " + ArchivePath);
                        CopyFilesRecursively(Path.Combine(sourcePath, Path.GetFileNameWithoutExtension(order).Split('$')[0]), ArchivePath);
                        VerfiyAndRemove(Path.Combine(sourcePath, Path.GetFileNameWithoutExtension(order).Split('$')[0]), ArchivePath);
                    }
                    
                }
            }

        }
        protected static void InitializePhotoWatcher()
        {
            try
            {
                Photowatcher = new FileSystemWatcher();
                Photowatcher.Path = PhotoWatchFolder;
                Photowatcher.EnableRaisingEvents = true;
                Photowatcher.IncludeSubdirectories = false;
                Photowatcher.InternalBufferSize = 64000;
                Photowatcher.Created += PhotoOnCreated;
                Photowatcher.Error += OnError;
            }
            catch(Exception ex)
            {
                string msg = "Error in Inlitialize Photo Watcher method, message: " + ex.Message + "\n stack trace : " + ex.StackTrace;
                Log(msg);
                Email(msg);
            }
        }

        protected static void InitializeAlbumWatcher()
        {
            try
            {
                Albumewatcher = new FileSystemWatcher();
                Albumewatcher.Path = AlbumWatchFolder;
                Albumewatcher.EnableRaisingEvents = true;
                Albumewatcher.IncludeSubdirectories = false;
                Albumewatcher.InternalBufferSize = 64000;
                Albumewatcher.Created += AlbumOnCreated;
                Albumewatcher.Error += OnError;
            }
            catch (Exception ex)
            {
                string msg = "Error in Inlitialize Album Watcher method, message: " + ex.Message + "\n stack trace : " + ex.StackTrace;
                Log(msg);
                Email(msg);
            }
        }
      
        private static bool CheckFolderRecursiveUsingOrderDetials(string Fullpath)
        {
            try
            {
                string FolderName = Path.GetFileName(Fullpath);
                var files = Directory.GetFiles(OrderDetailsFolder, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (Path.GetFileName(file).StartsWith(FolderName))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                string msg = "Error in CheckFolderRecursiveUsingOrderDetials method, message: " + ex.Message + "\n stack trace : " + ex.StackTrace;
                Log(msg);
                Email(msg);
            }
            return false;
        }
        private static void PhotoOnCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (CheckFolderRecursiveUsingOrderDetials(e.FullPath))
                { return; }
                int NextOrderNum = 1;
                Log(e.FullPath);
                string MovePath = String.Empty;

                while (true)
                {
                    MovePath = PhotoPrintFolderPath + @"\"+ PhotoCode +DateTime.Now.ToString("_yyyy_MM_dd_") + NextOrderNum;
                    if (Directory.Exists(MovePath))
                    { NextOrderNum++; }
                    else
                    { break; }
                }

                while (true)
                {
                    int fCountA = Directory.GetFiles(e.FullPath, "*", SearchOption.AllDirectories).Length;
                    Thread.Sleep(20000);
                    int fCountB = Directory.GetFiles(e.FullPath, "*", SearchOption.AllDirectories).Length;
                    if (fCountA == fCountB)
                    { break; }
                }
               
                Directory.CreateDirectory(MovePath);
                Log("copying folder");
                CopyFilesRecursively(e.FullPath, MovePath);
                Log("copied to photo print folder");
                Log("verifying files");
                VerfiyAndRemove(e.FullPath, MovePath);
                Log("Files Verfied And Folder Removed");
                Log("creating order");
                CreateNewOrder(Path.GetFileName(MovePath) + ConfigurationManager.AppSettings["PendingOrder"], MovePath);
                Log("Order Created!!");
                
                GC.Collect();

              
            }

            catch (Exception ex)
            {
                string msg = "Error in PhotoOnCreated method, message: " + ex.Message + "\n stack trace : " + ex.StackTrace;
                
                Log(msg);
                Email(msg);
            }
        }
        private static void AlbumOnCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (CheckFolderRecursiveUsingOrderDetials(e.FullPath))
                { return; }

                int NextOrderNum = 1;
                Log(e.FullPath);
                string MovePath = String.Empty;
               
                while (true)
                {
                    MovePath = AlbumPrintFolderPath + @"\" + AlbumCode +DateTime.Now.ToString("_yyyy_MM_dd_") + NextOrderNum;
                    if (Directory.Exists(MovePath))
                    { NextOrderNum++; }
                    else
                    { break; }
                }

                while (true)
                {
                    int fCountA = Directory.GetFiles(e.FullPath, "*", SearchOption.AllDirectories).Length;
                    Thread.Sleep(20000);
                    int fCountB = Directory.GetFiles(e.FullPath, "*", SearchOption.AllDirectories).Length;
                    if (fCountA == fCountB)
                    { break; }
                }
               
                Directory.CreateDirectory(MovePath);
                Log("copying folder");
                CopyFilesRecursively(e.FullPath, MovePath);
                Log("copied to Album print folder");
                Log("verifying files");
                VerfiyAndRemove(e.FullPath, MovePath);
                Log("Files Verfied And Folder Removed");
                Log("creating order");
                CreateNewOrder(Path.GetFileName(MovePath) + ConfigurationManager.AppSettings["PendingOrder"], MovePath);
                Log("Order created!!!");
                GC.Collect();
               
            }
            catch (Exception ex)
            {
                string msg = "Error in AlbumOnCreated method, message: " + ex.Message + "\n stack trace : " + ex.StackTrace;

                Log(msg);
                Email(msg);
            }
        }

        private static void OnError(object sender, ErrorEventArgs e)
        {
            Email("Error in file system watcher (OnError Method, message: " + e.GetException().ToString());
            Log(e.GetException().ToString());
            Setup();

        }
      
        private static void CreateNewOrder(string OrderDetailsPath,string CopiedFilePath)
        {
            try
            {
                CopiedFilePath = Directory.GetDirectories(CopiedFilePath)[0];
                string[] files = Directory.GetFiles(CopiedFilePath, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    files[i] = Path.GetFileName(files[i]);
                }

                string FilePath = Path.Combine(OrderDetailsFolder, OrderDetailsPath);

                string data = $"Folder Name : {Path.GetFileName(CopiedFilePath)}\n";
                data += $"Files Count : {files.Length}\n";
                string CommaSeperatedFileNames = string.Join(",", files);
                data += $"Files : {CommaSeperatedFileNames}\n";
                File.AppendAllText(FilePath, data);

            }
            catch (Exception ex)
            {
                string msg = "Error in Create New Order method, message: " + ex.Message + "\n stack trace : " + ex.StackTrace;
                Log(msg);
                Email(msg);
            }
        }

        private static void VerfiyAndRemove(string source, string destination)
        {
            try
            {
                if (DirSize(destination) == DirSize(source))
                {
                    try
                    {
                        Directory.Delete(source, true);
                    }
                    catch (Exception ex)
                    {
                        Log(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = "Error in Verify And Remove method, message: " + ex.Message + "\n stack trace : " + ex.StackTrace;
                Log(msg);
                Email(msg);
            }

        }

        public static long DirSize(string path)
        {
            try
            {
                DirectoryInfo d = new DirectoryInfo(path);
                long size = 0;
                // Add file sizes.
                FileInfo[] fis = d.GetFiles();
                foreach (FileInfo fi in fis)
                {
                    size += fi.Length;
                }
                // Add subdirectory sizes.
                DirectoryInfo[] dis = d.GetDirectories();
                foreach (DirectoryInfo di in dis)
                {
                    size += DirSize(di.FullName);
                }
                return size;
            }
            catch (Exception ex)
            {
                string msg = "Error in DirSize method, message: " + ex.Message + "\n stack trace : " + ex.StackTrace;
                Log(msg);
                Email(msg);
            }
            return 0;
        }

        private static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            try
            {
                FilesCount = 0;
                FileNames = "";
                targetPath = Path.Combine(targetPath, Path.GetFileName(sourcePath));
                Directory.CreateDirectory(targetPath);
                foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
                }

                foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                {
                    FilesCount++;
                    FileNames += "," + Path.GetFileName(newPath);
                    File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
                }
            }
            catch (Exception ex)
            {
                string msg = "Error in CopyFilesRecursivelyr method, message: " + ex.Message + "\n stack trace : " + ex.StackTrace;
                Log(msg);
                Email(msg);
            }
        }

        private static void Log(string Message)
        {
            string fpath = LogPath + @"\"+DateTime.Now.ToString("dd_MM_yyyy")+"_ServiceApp_log.txt";
            File.AppendAllText(fpath, DateTime.Now.ToString()+" : " + Message + "\n");
        }
        public void Stop()
        {
            // write code here that runs when the Windows Service stops.  
        }   

    }
}

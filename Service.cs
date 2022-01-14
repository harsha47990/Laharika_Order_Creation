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

namespace StoresDBCheckService
{
    class Service
    {
       
        public static string PhotoWatchFolder,AlbumWatchFolder, TimerEventCopyPasteSources, TimerEventCopyPasteDestinations;
        public static string OrderDetailsFolder,PhotoPrintFolderPath,AlbumPrintFolderPath,LogPath;
        private static FileSystemWatcher Photowatcher, Albumewatcher;
        public static string FileNames;
        public static int FilesCount, ArchiveDayCount;
        public static System.Timers.Timer timer;

        private static List<string> CopiedFiles = new List<string>();
        public static string SQLConnectionString;
        public static DataTable ExcelData;
        public static bool ChangeDay = true;
        //= "Data Source=jliq-sql012A,51001;Initial Catalog = StoresCheckService; User ID = it3testuser; Password = Shell@123;";
        public Service()
        {
            Log("service Started");
            ReadConfigSettings();
            int timeout = 60000 * 60;
            timeout = 60000*1;
            timer = new System.Timers.Timer(timeout);
            timer.Elapsed += TimerElapsed;
            timer.AutoReset = true;
            timer.Enabled = true;
            
           
        }
        private static void Setup()
        {
            ReadConfigSettings();
            InitializePhotoWatcher();
            InitializeAlbumWatcher();
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
            ArchiveDayCount = Convert.ToInt32(ConfigurationManager.AppSettings["ArchiveDayCount"]);
            LogPath = ConfigurationManager.AppSettings["LogPath"];
        }

        private static void TimerElapsed(Object source, ElapsedEventArgs e)
        {
            if (Convert.ToInt32(DateTime.Now.ToString("HH")) == 1)
            {
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

                cleanFolders(AlbumPrintFolderPath);
                cleanFolders(PhotoPrintFolderPath);

            }
        }
        
        private static void cleanFolders(string sourcePath)
        {
            foreach (string path in Directory.GetDirectories(sourcePath))
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(path);
                var orderdate = directoryInfo.CreationTime;
                
                if((DateTime.Now - orderdate).Days > 90)
                {
                    Directory.Delete(path, true);
                }
            }
        }
        protected static void InitializePhotoWatcher()
        {
            Photowatcher = new FileSystemWatcher();
            Photowatcher.Path = PhotoWatchFolder;
            Photowatcher.EnableRaisingEvents = true;
            Photowatcher.IncludeSubdirectories = false;
            Photowatcher.InternalBufferSize = 64000;
            Photowatcher.Created += PhotoOnCreated;
            Photowatcher.Error += OnError;
        }

        protected static void InitializeAlbumWatcher()
        {
            Albumewatcher = new FileSystemWatcher();
            Albumewatcher.Path = AlbumWatchFolder;
            Albumewatcher.EnableRaisingEvents = true;
            Albumewatcher.IncludeSubdirectories = false;
            Albumewatcher.InternalBufferSize = 64000;
            Albumewatcher.Created += AlbumOnCreated;
            Albumewatcher.Error += OnError;
        }
      
        private static bool CheckFolderRecursiveUsingOrderDetials(string Fullpath)
        {
            string FolderName = Path.GetFileName(Fullpath);
            var files = Directory.GetFiles(OrderDetailsFolder, "*", SearchOption.AllDirectories);
            foreach(var file in files)
            {
                if(Path.GetFileName(file).StartsWith(FolderName))
                {
                    return true;
                }
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
                    MovePath = PhotoPrintFolderPath + @"\"+ DateTime.Now.ToString("2_yyyy_MM_dd_") + NextOrderNum;
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
                CopyFilesRecursively(e.FullPath, MovePath);
                Log("copied to photo print");
                VerfiyAndRemove(e.FullPath, MovePath);
                Log("Files Verfied And Folder Removed");
                CreateNewOrder(Path.GetFileName(MovePath) + ConfigurationManager.AppSettings["PendingOrder"], MovePath);
                Log("Order Created!!");
                
                GC.Collect();

              
            }
            catch(Exception ex)
            {
                Log(ex.Message);
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
                    MovePath = AlbumPrintFolderPath + @"\" + DateTime.Now.ToString("1_yyyy_MM_dd_") + NextOrderNum;
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
                CopyFilesRecursively(e.FullPath, MovePath);
                Log("copied to Album print");
                VerfiyAndRemove(e.FullPath, MovePath);
                Log("Files Verfied And Folder Removed");
                CreateNewOrder(Path.GetFileName(MovePath) + ConfigurationManager.AppSettings["PendingOrder"], MovePath);
                Log("Order created!!!");
                GC.Collect();
               
            }
            catch(Exception ex)
            {
                Log(ex.Message);
            }
        }

        private static void OnError(object sender, ErrorEventArgs e)
        {
            Log(e.GetException().ToString());
            Setup();
        }
      
        private static void CreateNewOrder(string OrderDetailsPath,string CopiedFilePath)
        {
            CopiedFilePath = Directory.GetDirectories(CopiedFilePath)[0];
            string[] files = Directory.GetFiles(CopiedFilePath, "*", SearchOption.AllDirectories);
            for(int i = 0; i<files.Length;i++)
            {
                files[i] = Path.GetFileName(files[i]);
            }
            string waitMsg = Path.GetFileNameWithoutExtension(OrderDetailsPath) + "Wait.txt";
            waitMsg = Path.Combine(OrderDetailsFolder, waitMsg);
            string FilePath = Path.Combine(OrderDetailsFolder, OrderDetailsPath);
          
             File.AppendAllText(waitMsg, $"Folder Name : {Path.GetFileName(CopiedFilePath)}\n");
             File.AppendAllText(waitMsg, $"Files Count : {files.Length}\n");
             string CommaSeperatedFileNames = string.Join(",", files);
             File.AppendAllText(waitMsg, $"Files : {CommaSeperatedFileNames}\n");
             File.Move(waitMsg, FilePath);
        }

        private static void VerfiyAndRemove(string source, string destination)
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

        public static long DirSize(string path)
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

        private static void CopyFilesRecursively(string sourcePath, string targetPath)
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

        private static void Log(string Message)
        {
            string fpath = LogPath + @"\log.txt";
            File.AppendAllText(fpath, DateTime.Now.ToString()+" : " + Message + "\n");
        }
        public void Stop()
        {
            // write code here that runs when the Windows Service stops.  
        }   

     
    }
}

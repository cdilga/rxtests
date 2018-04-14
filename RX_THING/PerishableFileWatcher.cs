using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwistedOak.Collections;
using TwistedOak.Util;

namespace RX_THING
{
    public class PerishableFileSystem : IDisposable
    {
        private readonly FSWatcherWrapper watcher;

        public PerishableFileSystem(string directory, string extension, bool includeSubdirectories = true) {
            var filter = "*." + extension.Trim('.');
            watcher = new FSWatcherWrapper(directory, filter, includeSubdirectories)
            {
                Host = this
            };

            addDir(directory);

            void addDir(string dirPath) {
                if(includeSubdirectories) {
                    foreach(var dir in Directory.GetDirectories(dirPath)) {
                        addDir(dir);
                    }
                }
                foreach(var file in Directory.GetFiles(dirPath, filter)) {
                    var fileFull = Path.GetFullPath(file);
                    var source = new LifetimeSource();
                    if(FileSouls.TryAdd(fileFull, source)) {
                        AllFiles.Add(fileFull, source.Lifetime);
                    }
                    else {
                        OnError?.Invoke(this,
                        new ErrorEventArgs(new Exception("Unknown error occurred while getting initial files.")));
                    }
                }
            }
        }


        ConcurrentDictionary<string, LifetimeSource> FileSouls = new ConcurrentDictionary<string, LifetimeSource>(3, 20);

        private readonly PerishableCollection<string> AllFiles = new PerishableCollection<string>();

        public IObservable<Perishable<string>> CurrentAndFutureFiles() => AllFiles.CurrentAndFutureItems();

        public void Dispose() {
            watcher.Dispose();
            foreach(var source in FileSouls.Values) {
                source.ImmortalizeLifetime();
            }
        }

        private void fileSystemWatcher_Changed(object sender, FileSystemEventArgs e) {
            fileSystemWatcher_Deleted(sender, e);
            fileSystemWatcher_Created(sender, e);
        }
        private void fileSystemWatcher_Created(object sender, FileSystemEventArgs e) {
            if(!Path.HasExtension(e.FullPath)) {
                addDir(e.FullPath);
            }
            else {
                addFile(e.FullPath);
            }

            void addDir(string dir) {
                foreach(var subdir in Directory.GetDirectories(dir)) {
                    addDir(subdir);
                }
                foreach(var file in Directory.GetFiles(dir)) {
                    addDir(file);
                }
            }
            void addFile(string file) {
                if(FileSouls.ContainsKey(file)) {
                    fileSystemWatcher_Changed(sender, e);
                }
                else {
                    var source = new LifetimeSource();
                    if(FileSouls.TryAdd(file, source)) {
                        AllFiles.Add(file, source.Lifetime);
                    }
                    else {
                        OnError?.Invoke(this,
                            new ErrorEventArgs(new Exception("Unknown error occurred when file was created")));
                    }
                }
            }
        }

        void addCreatedFile(string fullFileName) {
            var source = new LifetimeSource();
            //add the lifetime. If there is already one there (for some reason... there shouldn't be) then end it and use new one
            FileSouls.AddOrUpdate(fullFileName, source, (p, lfs) => { lfs.EndLifetime(); return source; });
            AllFiles.Add(fullFileName, source.Lifetime);
        }

        private void fileSystemWatcher_Deleted(object sender, FileSystemEventArgs e) {
            var file = e.FullPath;
            if(FileSouls.TryRemove(file, out var source)) {
                source.EndLifetime();
            }
        }
        private void fileSystemWatcher_Error(object sender, ErrorEventArgs e) {
            OnError?.Invoke(this, e);
        }
        private void fileSystemWatcher_Renamed(object sender, RenamedEventArgs e) {
            fileSystemWatcher_Deleted(sender, new FileSystemEventArgs(e.ChangeType, e.OldFullPath.Replace(e.OldName, ""), e.OldName));
            fileSystemWatcher_Created(sender, e);
        }

        public event EventHandler<ErrorEventArgs> OnError;





        private class FSWatcherWrapper : IDisposable
        {
            private readonly FileSystemWatcher fileSystemWatcher;
            public PerishableFileSystem Host { get; set; }

            public FSWatcherWrapper(string directory, string filter, bool includeSubdirectories) {
                fileSystemWatcher = new FileSystemWatcher(directory, filter);
                fileSystemWatcher.BeginInit();
                fileSystemWatcher.IncludeSubdirectories = includeSubdirectories;

                //pipe events through methods, not straight into Host, so that the Host
                //property is the ONLY handle on host.
                fileSystemWatcher.Changed += fileSystemWatcher_Changed;
                fileSystemWatcher.Created += fileSystemWatcher_Created;
                fileSystemWatcher.Deleted += fileSystemWatcher_Deleted;
                fileSystemWatcher.Error += fileSystemWatcher_Error;
                fileSystemWatcher.Renamed += fileSystemWatcher_Renamed;

                fileSystemWatcher.EnableRaisingEvents = true;
                fileSystemWatcher.EndInit();
            }

            private void fileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
                => Host?.fileSystemWatcher_Changed(sender, e);
            private void fileSystemWatcher_Created(object sender, FileSystemEventArgs e)
                => Host?.fileSystemWatcher_Created(sender, e);
            private void fileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
                => Host?.fileSystemWatcher_Deleted(sender, e);
            private void fileSystemWatcher_Error(object sender, ErrorEventArgs e)
                => Host?.fileSystemWatcher_Error(sender, e);
            private void fileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
                => Host?.fileSystemWatcher_Renamed(sender, e);


            public void Dispose() {
                //Don't dispose host as host is disposing this.
                Host = null;
                //Set host to null so that events can no longer propagate outward to
                //hopefully prevent the memory leak where even a disposed FSW keeps a handle on its events
                if(fileSystemWatcher != null) {
                    fileSystemWatcher.EnableRaisingEvents = false;
                    fileSystemWatcher.Dispose();
                }
            }
        }
    }
}

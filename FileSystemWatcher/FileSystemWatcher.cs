//------------------------------------------------------------------------------
// <copyright file="FileSystemWatcher.cs" Author="Abdelhamid Larachi">
//     Copyright (c) Abdelhamid Larachi.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileSystemWatcher
{

    /// <devdoc>
    ///    <para>  detect directory change and return renamed, moved, created, and deleted files.
    ///    Able to spot renamed files without hashcode comparing or real time tracking.  </para>
    /// </devdoc>


    public enum Operation
    {
        DELETED, CHANGED, RENAMED, MOVED
    }


    public class FileSystemInitializer
    {
        // watched directory files
        private string[] files;
        // watched directory
        private string directory;
        // hidden directory to store initial state
        private string initialstate;


        /// <summary>
        /// Sets file read access permission
        /// <param name="FileName"> file path </param>
        /// <param name="readOnly"> read attribute </param>
        /// </summary>


        private void SetFileReadAccess(string FileName, bool readOnly)
        {
            File.SetAttributes(FileName, File.GetAttributes(FileName) & (readOnly ? FileAttributes.ReadOnly : ~FileAttributes.ReadOnly));
        }


        /// <summary>
        /// Check if file is read-only
        /// <param name="FileName"> file path </param>
        /// <Returns> returns true if readable only</Returns>
        /// </summary>


        private bool IsFileReadOnly(string FileName)
        {
            return new FileInfo(FileName).IsReadOnly;
        }


        /// <summary>
        /// Get every file creation time in ticks
        /// <param name="paths"> directory files list </param>
        /// <Returns> list of creation time in ticks</Returns>
        /// </summary>


        private long[] GetAllFilesCreationTime(string[] paths)
        {
            List<long> creation_time = new List<long>();

            foreach (string path in paths)
                creation_time.Add(File.GetCreationTime(path).Ticks);

            return creation_time.ToArray();
        }



        /// <summary>
        /// Set creation time attribute for file.
        /// At this point at least FilePermission has already been demanded.
        /// <param name="FileName"> string file path </param>
        /// <param name="datetime"> creation time </param>
        /// <Exception>throw UnauthorizedAccessException if access is denied and file is dublicated</Exception>
        /// </summary>


        private void SetCreationTime(string FileName, DateTime datetime)
        {
            try
            {
                File.SetCreationTime(FileName, datetime);

            }
            catch (UnauthorizedAccessException)
            {

                /* if unauthorized & other file with same ticks 
                 * exists, then exit to prevent files conflict,
                 * if not then its safe to ignore */

                if (ExistsWithSameTicks(datetime.Ticks))
                    throw new UnauthorizedAccessException();
            }
        }


        /// <summary>
        /// Check if any file hold exact same creation time in ticks attributes
        /// <param name="value"> creation time by ticks </param>
        /// <returns>Return true if found</returns>
        /// </summary>


        private bool ExistsWithSameTicks(long value)
        {
            long[] ticks = GetAllFilesCreationTime(files);
            return ticks.ToList().Contains(value);
        }


        /// <summary>
        /// Check if any ticks value is dublicated in another file
        /// <param name="files"> directory files </param>
        /// <returns>Return true if found</returns>
        /// </summary>


        private bool HasSameTicks(string[] files)
        {
            long[] ticks = GetAllFilesCreationTime(files);
            return ticks.Length != ticks.Distinct().Count();
        }



        /// <summary>
        /// Copy sub-directories to initial directory
        /// <param name="ignored"> default/user ignored files </param>
        /// </summary>



        private void WriteDirectories(string[] ignored)
        {
            string[] directories = Directory.GetDirectories(directory, "*", SearchOption.AllDirectories).Where(dir => !ignored.Any(ignored => dir.Contains(ignored))).ToArray();

            foreach (string dirPath in directories)
            {
                if (!ignored.Any(ignored => dirPath.Contains(ignored)))
                    Directory.CreateDirectory(dirPath.Replace(directory, initialstate));
            }
        }



        /// <summary>
        /// Copy files to initial directory
        /// <param name="ignored"> default/user ignored files </param>
        /// </summary>


        private void WriteFiles(string[] ignored)
        {
            files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Where(dir => !ignored.Any(ignored => dir.Contains(ignored))).ToArray();
            bool _HasSameTicks = HasSameTicks(files);
            int tick = 0;

            foreach (string filename in files)
            {
                if (!ignored.Any(ignored => filename.Contains(ignored)))
                {
                    // get read access if file is read-only
                    if (!IsFileReadOnly(filename))
                        SetFileReadAccess(filename, false);

                    string path = filename.Replace(directory, initialstate);

                    /* add negligible n tick to file creation time to set a unique creation time 
                    for each file while we keep exact same creation time. */

                    DateTime creation_time = File.GetCreationTime(filename);
                    if (_HasSameTicks) { creation_time.AddTicks(tick); }

                    // copy file to initial state dir
                    File.Copy(filename, path, true);
                    // set same creation time on target
                    SetCreationTime(path, creation_time);

                    if (_HasSameTicks)
                    {
                        SetCreationTime(filename, creation_time);

                        /* 
                         * Rewrite files in directory, to force setting creation time.
                         * in some disks formats, setting creation time with permissions doesn't take change everytime,
                         * rewriting the file then setting creation time does the trick.
                         */

                        if (DateTime.Compare(File.GetCreationTime(filename), creation_time) != 0)
                        {
                            File.Copy(path, filename, true);
                            SetCreationTime(filename, creation_time);
                        }
                    }

                    tick++;
                }
            }
        }


        /// <summary>
        /// Take a directory snapshot before changes
        /// <param name="dir">directory source string</param>
        /// <param name="ignored"> default/user ignored files </param>
        /// </summary>



        public void Init(string dir, string[] ignored)
        {
            directory = dir;
            // set initial directory state in .initialstate 
            initialstate = Path.Combine(dir, ".initialstate");
            // create initial directory 
            Directory.CreateDirectory(initialstate);
            // copy directories
            WriteDirectories(ignored);
            // copy files
            WriteFiles(ignored);
        }

    }



    /**
     * * Class representing one directory changes Modal.
     */

    public class ChangeModel
    {
        public Changes Changes { get; set; }
        // returns true if directory has changed
        public bool HasChanged { get; set; }
    }


    /**
     * * Class representing changes Object lists.
     */

    public class Changes
    {
        public List<Renamed> renamed { get; set; }
        public List<Rewritten> changed { get; set; }
        public List<string> deleted { get; set; }
        public List<string> created { get; set; }
    }


    /**
     * * Class representing Renamed file Objects.
     */

    public class Renamed
    {
        // previous name
        public string prevName { get; set; }
        // new / current name
        public string name { get; set; }
        // returns true if file is moved
        public bool moved { get; set; }
        // returns % of renamed action accuracy
        public double similarity { get; set; }
    }


    /**
     * * Class representing Changed/Rewritten file Objects.
     */

    public class Rewritten
    {
        public string filename { get; set; }
        // % similarity with old content
        public double match { get; set; }
    }




    public static class FileSystemWatcher
    {
        // system and other files to ignore

        readonly static string[] ignore_default = {
            // dependency_caches
            "/node_modules", "/packages",
            // compiled_code
            ".pyc", ".o", ".class",
            // build_output_dir
            "/bin", "/out", "/target",
            // runtime_files
            ".log", ".lock", ".tmp",
            // system_files
            ".DS_Store", "Thumbs.db",
            // config_files
            ".idea/workspace.xml",
            // git
            ".git", ".watchmanconfig", ".initialstate"
        };


        /*
        * Initialize directory, create snapshot, get permissions.
        * @param directory path of directory.
        * @param ignore contains list of ignored patterns.
        */

        public static void BeginInit(string directory, string[] ignore = null)
        {
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException();

            // Concat default with user input
            if (ignore == null) { ignore = new string[] { }; }
            ignore = ignore_default.Union(ignore).ToArray();

            new FileSystemInitializer().Init(directory, ignore);
        }


        /*
        * Get directory files action by groupe (created, deleted, renamed, changed)
        * @param directory path of initialized directory.
        * @return RENAMED, CREATED, DELETED, CHANGED files Object list.
        */

        public static ChangeModel EndInit(string directory)
        {
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException();

            return new FileSystemChangeHandler().End(directory, ignore_default);
        }

    }





    public class FileSystemChangeHandler
    {
        // watched directory
        private string directory;
        // hidden directory to store initial state
        private string initialstate;
        // initial directory files 
        private string[] initialDir;
        // current/new directory files
        private string[] CurrentDir;


        /// <summary>
        /// Calculate percentage similarity of two strings
        /// <param name="source">Source String to Compare with</param>
        /// <param name="target">Targeted String to Compare</param>
        /// <returns>Return Similarity between two strings from 0 to 1.0</returns>
        /// </summary>


        public double CalculateSimilarity(string source, string target)
        {
            if ((source == null) || (target == null)) return 0.0;
            if ((source.Length == 0) || (target.Length == 0)) return 0.0;
            if (source == target) return 100.0;

            string[] initial = source.Split(" ");
            string[] final = target.Split(" ");

            if (source.Length > target.Length) return initial.Count(x => final.Contains(x)) * 100 / initial.Length;
            else return final.Count(x => initial.Contains(x)) * 100 / final.Length;

        }

        /*
        * Convert full path list to relative path list
        * @param fullpaths full path list.
        * @param relativeTo directory which they are relative to.
        * @return relative path list.
        */

        public List<string> Pretify(List<string> fullpaths, string relativeTo)
        {
            for (int i = 0; i < fullpaths.Count; i++)
                fullpaths[i] = Path.GetRelativePath(relativeTo, fullpaths[i]);
            // relative paths
            return fullpaths;
        }

        /*
        * Get directory files action by groupe (created, deleted, renamed, changed)
        * @param dir path of initialized directory.
        * @param ignored contains default ignored system files.
        * @return RENAMED, CREATED, DELETED, CHANGED files Object list.
        */


        public ChangeModel End(string dir, string[] ignored)
        {
            if (!Directory.Exists(dir))
                throw new DirectoryNotFoundException();

            // get watched directory
            directory = dir;
            // get initial snapshot of watched directory
            initialstate = Path.Combine(dir, ".initialstate");

            // Get initial and final directory snapshot
            initialDir = GetInitialDirectoryFiles();
            CurrentDir = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Where(dir => !ignored.Any(ignored => dir.Contains(ignored))).ToArray();

            // Get differance between initial and final directory
            List<string> created = CurrentDir.Except(initialDir).ToList();
            List<string> deleted = initialDir.Except(CurrentDir).ToList();

            // Get renamed files from created & deleted
            List<Renamed> renamed = GetRenamed(created, deleted);

            // Filter renamed files from created & deleted and 
            created = Pretify(created, directory).Select(x => x).Except(renamed.Select(y => y.name)).ToList();
            deleted = Pretify(deleted, directory).Select(x => x).Except(renamed.Select(y => y.prevName)).ToList();

            List<Rewritten> rewritten = GetRewritten(created);

            return new ChangeModel
            {
                HasChanged = (created.Count + deleted.Count + renamed.Count + rewritten.Count) != 0,
                Changes = new Changes
                {
                    created = created,
                    deleted = deleted,
                    renamed = renamed,
                    // Get rewritten files filtred from created files
                    changed = rewritten
                }
            };
        }



        /*
        * Convert directory filename to initial directory filename
        * @return initial filename.
        */

        public string GetInitialFilename(string filename)
        {
            return filename.Replace(directory, initialstate);
        }

        /*
        * Convert initial directory filename to current directory filename
        * @return directory filename.
        */

        public string GetDirectoryFilename(string filename)
        {
            return filename.Replace(initialstate, directory);
        }

        /*
        * Get Initial Directory files attributes with current directory filenames
        * @return initial directory.
        */

        public string[] GetInitialDirectoryFiles()
        {
            string[] initial = Directory.GetFiles(initialstate, "*", SearchOption.AllDirectories);

            for (int i = 0; i < initial.Length; i++)
            {
                initial[i] = GetDirectoryFilename(initial[i]);
                Console.WriteLine(initial[i]);
            }

            return initial;
        }



        /*
        * Find highest match between a groupe of files
        * @param matches list of files with same creation time.
        * @param file filename of processing file.
        * @return (string filename with highest match, similarity percentage).
        */



        public (string, double) GetHighestMatch(List<string> matches, string file)
        {
            if (matches.Count > 1)
            {
                List<double> values = new List<double>();

                foreach (string match in matches)
                    values.Add(CalculateSimilarity(File.ReadAllText(file), File.ReadAllText(match)));
                return (matches[values.IndexOf(values.Max())], values.Max());
            }
            else
                return (matches[0], 100);
        }



        /*
        * a renamed file is recognized as deleted then created file
        * Extract renamed files from deleted and created arrays, using file's creation time ticks as an identifier.
        * (MacOs) on dublicate when user create a new file by copying an old one, will also copy the exact same attributes which will create a conflict between files because of dublicated ticks, 
        * while on (windows) it will set a new creation time for the file.
        * To prevent MacOS conflicts, on multiple matches, files will be compared and take out the file with more similarity.
        * similarity object property will be needed when file is not 100% renamed.
        * @param created files considered as created.
        * @param deleted files considered as deleted.
        * @return List of renamed objects.
        */


        public List<Renamed> GetRenamed(List<string> created, List<string> deleted)
        {

            List<Renamed> renamed = new List<Renamed>();

            foreach (string f in deleted)
            {
                // get file path on directory
                string file = GetInitialFilename(f);
                // get file creation time by ticks
                long ticks = File.GetCreationTime(file).Ticks;
                // find all files with same ticks value
                List<string> matches = created.FindAll(filename => File.GetCreationTime(filename).Ticks == ticks);


                if (matches.Count != 0)
                {
                    // if multiple matches return highest match.
                    (string, double) res = GetHighestMatch(matches, file);
                    string filename = res.Item1;
                    double match = res.Item2;

                    // get relative path in initial directory
                    string prevName = Path.GetRelativePath(initialstate, file);
                    // get relative path in current directory
                    string name = Path.GetRelativePath(directory, filename);

                    if (prevName != name)
                    {
                        renamed.Add(
                            new Renamed
                            {
                                prevName = prevName,
                                name = name,
                                // if file is moved or renamed
                                moved = Path.GetFileName(file) == Path.GetFileName(filename),
                                // % of renamed action
                                similarity = match
                            });
                    }
                }
            }

            return renamed;
        }


        /*
        * Compare each file with every initial directory file. Simplifies the problem by
        * kicking out created files.
        * Calculate match percentage between files if its changed.
        * @param created files considered as created.
        * @return List of rewritten objects.
        */

        public List<Rewritten> GetRewritten(List<string> created)
        {
            List<Rewritten> rewritten = new List<Rewritten>();

            foreach (string file in CurrentDir)
            {
                string filename = Path.GetRelativePath(directory, file);

                if (!created.Contains(filename))
                {
                    // get creation time ticks for current file
                    long ticks = File.GetCreationTime(file).Ticks;
                    // find if file exists in initial directory
                    int initialFileIndex = FindByTicks(ticks);

                    // if file has initial content
                    if (initialFileIndex > -1)
                    {
                        // get initial content of file
                        string initial = File.ReadAllText(initialDir[initialFileIndex]);
                        // get final content of file
                        string final = File.ReadAllText(file);
                        // calculate required steps to for old content to match new content
                        double match = CalculateSimilarity(initial, final);

                        // if less than 100%
                        if (match < 100)
                        {
                            rewritten.Add(
                                new Rewritten
                                {
                                    filename = filename,
                                    match = match
                                });
                        }
                    }
                }
            }
            return rewritten;
        }



        /*
        * Find file by its creation time ticks
        * @param ticks creation time value.
        * @return index of file in directory array.
        */


        public int FindByTicks(long ticks)
        {
            initialDir = Directory.GetFiles(initialstate, "*", SearchOption.AllDirectories);
            return Array.FindIndex(initialDir, filename => File.GetCreationTime(filename).Ticks == ticks);
        }
    }
}

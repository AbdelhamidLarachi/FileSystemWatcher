FileSystemWatcher [![FileSystemWatcher License](https://img.shields.io/badge/LICENSE-MIT-brightgreen)](https://github.com/AbdelhamidLarachi/FileSystemWatcher/blob/master/FileSystemWatcher/LICENSE)[![FileSystemWatcher NuGet version](https://img.shields.io/badge/nuget-v1.0.0-blue)](https://www.nuget.org/packages/AL-FileSystemWatcher/)
========

FileSystemWatcher is C# library to detect local directories change and return renamed, moved, created, and deleted files. Able to spot renamed files without hashcode comparing or real time tracking. It targets `netstandard2.1+`.

## Examples

example on how to use the library:

All you have to do is to initialize the local directory you want to watch, using : 

```csharp
namespace FileSystemWatcher
{
    class Program
    {
        static void Main(string[] args)
        {
            // Choose a directory to watch
            string directory = @"/Users/nginx-iwnl/Desktop/Project";

            // Initialize directory 
            FileSystemWatcher.BeginInit(directory);

            // Optional: using ignore patterns
            string[] ignore = { ".exe", "/node_modules" };
            FileSystemWatcher.BeginInit(directory, ignore);
        }
    }
}
```

Then modify the directory content, after that you call EndInit to get result : 

```csharp
using System;

namespace FileSystemWatcher
{
    class Program
    {
        static void Main(string[] args)
        {
            // Choose a directory to watch
            string directory = @"/Users/nginx-iwnl/Desktop/Project";

            // End directory watch
            ChangeModel result = FileSystemWatcher.EndInit(directory);

            // Print Result
            Console.WriteLine("Directory has changed : {0}", result.HasChanged);
            Console.WriteLine("\n Created Files:");

            foreach (var file in result.Changes.created)
                Console.WriteLine(file);

            Console.WriteLine("\n Deleted Files:");

            foreach (var file in result.Changes.deleted)
                Console.WriteLine(file);

            Console.WriteLine("\n Renamed Files:");

            foreach (var file in result.Changes.renamed)
                Console.WriteLine("{0} => {1}", file.prevName, file.name);

            Console.WriteLine("\n Changed Files:");

            foreach (var file in result.Changes.changed)
                Console.WriteLine("{0} with {1}% match", file.filename, file.match);
        }
    }
}

```


provided implementations:

- `Created files`
- `Deleted files`
- `Renamed files`
- `Moved files`
- `Changed files with similarity percentage %`

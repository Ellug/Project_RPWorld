using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class MapFileUtility
{
    public static string[] GetMapNamesFromDisk()
    {
        var directory = Path.Combine(Application.persistentDataPath, "Maps");
        if (!Directory.Exists(directory))
            return Array.Empty<string>();

        var files = Directory.GetFiles(directory, "*.json");
        if (files == null || files.Length == 0)
            return Array.Empty<string>();

        var names = new List<string>(files.Length);
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names.ToArray();
    }
}

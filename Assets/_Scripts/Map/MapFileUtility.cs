using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 맵 파일 관련 유틸리티. 저장 경로: {persistentDataPath}/Maps/*.json
public static class MapFileUtility
{
    // 디스크에 저장된 맵 이름 목록 반환 (알파벳순 정렬)
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

using System.Text;

namespace C47.Core;

/// <summary>Conteneur .p47 : 6 lignes d'en-tête puis un octet décimal par ligne (saveRestorePrograms.c).</summary>
public static class P47File
{
    /// <summary>Lit les octets du programme, sans le .END. final.</summary>
    public static byte[] Read(byte[] file)
    {
        var l = Encoding.ASCII.GetString(file).Split((char[])['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (l.Length < 6 || l[0] != "PROGRAM_FILE_FORMAT" || !l[2].EndsWith("_program_file_version") || l[4] != "PROGRAM")
            throw new InvalidDataException("Ce fichier n'est pas un programme C47 (.p47)");
        int n = int.Parse(l[5]);
        if (l.Length < 6 + n)
            throw new InvalidDataException($"Fichier .p47 tronqué : {n} octets annoncés, {l.Length - 6} présents");
        var d = new byte[n];
        for (int i = 0; i < n; i++) d[i] = byte.Parse(l[6 + i]);
        return d;
    }

    /// <summary>Écrit comme WRITEP : taille sans le .END., puis les octets et 255 255.</summary>
    public static byte[] Write(byte[] program)
    {
        var s = new StringBuilder("PROGRAM_FILE_FORMAT\n0\nC47_program_file_version\n1\nPROGRAM\n");
        s.Append(program.Length).Append('\n');
        foreach (var b in program) s.Append(b).Append('\n');
        s.Append("255\n255\n");
        return Encoding.ASCII.GetBytes(s.ToString());
    }
}

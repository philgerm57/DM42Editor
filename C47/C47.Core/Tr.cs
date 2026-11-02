using System.Text.RegularExpressions;

namespace C47.Core;

/// <summary>Messages de l'encodeur : le français sert de clé, l'anglais est appliqué à l'affichage.</summary>
public static class Tr
{
    public static bool English;

    static readonly (Regex From, string To)[] table = new (string, string)[]
    {
        (@"^Instruction inconnue : (.*)$", "Unknown instruction: $1"),
        (@"^Chaîne non fermée$", "Unterminated string"),
        (@"^Chaîne trop longue \(255 octets au plus\)$", "String too long (255 bytes at most)"),
        (@"^Écrivez la valeur directement, sans LITE$", "Write the value directly, without LITE"),
        (@"^Registre indirect invalide : (.*)$", "Invalid indirect register: $1"),
        (@"^Nom d'étiquette trop long : (.*)$", "Label name too long: $1"),
        (@"^Base invalide : (.*)$", "Invalid base: $1"),
        (@"^Nombre invalide : (.*)$", "Invalid number: $1"),
        (@"^Programme trop long : (.*) octets$", "Program too long: $1 bytes"),
        (@"^caractère (.*) impossible dans une chaîne C47$", "Character $1 cannot be stored in a C47 string"),
        (@"^Ce fichier n'est pas un programme C47 \(\.p47\)$", "This file is not a C47 program (.p47)"),
        (@"^Fichier \.p47 tronqué : (.*) octets annoncés, (.*) présents$", "Truncated .p47 file: $1 bytes declared, $2 found"),
        (@"^(.*) sans (.*) correspondant$", "$1 without a matching $2"),
        (@"^(.*) : paramètre manquant$", "$1: missing parameter"),
        (@"^(.*) : nombre de pas manquant$", "$1: missing step count"),
        (@"^(.*) : numéro de structure invalide$", "$1: invalid structure number"),
        (@"^(.*) : texte entre ‘ ’ attendu$", "$1: text between ‘ ’ expected"),
        (@"^(.*) : nom de menu entre ‘ ’ attendu$", "$1: menu name between ‘ ’ expected"),
        (@"^(.*) : GTO ou XEQ attendu, pas (.*)$", "$1: GTO or XEQ expected, not $2"),
        (@"^(.*) : (.*) n'est pas accepté ici$", "$1: $2 is not accepted here"),
        (@"^(.*) : 4 lettres parmi x y z t attendues$", "$1: 4 letters among x y z t expected"),
        (@"^(.*) : étiquette invalide : (.*)$", "$1: invalid label: $2"),
        (@"^(.*) : registre invalide : (.*)$", "$1: invalid register: $2"),
        (@"^(.*) : indicateur système inconnu : (.*)$", "$1: unknown system flag: $2"),
        (@"^(.*) : indicateur invalide : (.*)$", "$1: invalid flag: $2"),
        (@"^(.*) : indicateur local limité à \.31$", "$1: local flags stop at .31"),
        (@"^(.*) : nombre attendu, pas (.*)$", "$1: number expected, not $2"),
        (@"^(.*) : (.*) dépasse (.*)$", "$1: $2 exceeds $3"),
        (@"^(.*) : (.*) est réservé aux formes indirectes$", "$1: $2 is reserved for indirect forms"),
    }.Select(p => (new Regex(p.Item1), p.Item2)).ToArray();

    public static string T(string fr)
    {
        if (!English) return fr;
        foreach (var (from, to) in table)
            if (from.IsMatch(fr)) return from.Replace(fr, to);
        return fr;
    }
}

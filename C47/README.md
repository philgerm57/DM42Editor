# C47 : encodeur et décodeur de programmes .p47

Bibliothèque `C47.Core` et outil en ligne de commande `c47`, sans dépendance externe.

```bash
dotnet run --project C47.Cli -- enc programme.txt programme.p47
dotnet run --project C47.Cli -- dec programme.p47
dotnet run --project C47.Cli -- test G:/c47/c43
```

## Syntaxe

La syntaxe suit les listings `.p47u` de `rejig` et les exports `XPORTP` :

- `LBL ‘NOM’`, `STO 01`, `RCL →X`, `XEQ IND 05`, `FS? .01` ; plusieurs pas possibles par ligne (`5 3 +`).
- Nombres : `5` (entier long), `1.5e3` (réel), `111#2` ou `3₁₆` (entier court), `1.5°` (angle).
- Chaîne : `‘TEXTE’`, `"TEXTE"` ou `'TEXTE'`.
- `# texte` reste dans le programme (pas `REM`) ; `@ texte` et `; texte` sont ignorés.
- Les noms de `items.c` (`x≠ ?`) et les noms courants (`X≠0?`, `X<>Y`, `SQRT`) sont acceptés : voir `C47.Core/c47_alias.tsv`.
- `IF`, `ELSE`, `DO`, etc. sont numérotés automatiquement comme le fait `VALID`.
- `⟦hex⟧` insère des octets bruts : le décodeur s'en sert pour les littéraux binaires.
- `ITM:n` désigne une instruction par son numéro quand plusieurs partagent un nom.

## Licence

GNU GPLv3, comme DM42 Editor. `C47.Core/c47_items.tsv` et les règles d'encodage sont dérivés des sources de C47
([rpncalculators/c43](https://gitlab.com/rpncalculators/c43), GPL-3.0-only, Copyright The C47 Authors).

Les descriptions de l'aide (`C47.Core/c47_help.tsv`, colonne `doc_curated` de `docs/refdb47`) sont de RJvM,
sous licence GNU FDL 1.3 ou ultérieure, sans sections invariantes. Le fichier garde l'original anglais et sa traduction
française (`tools/help_fr.tsv`, appliquée par `tools/translate_help.py`) ; en français, l'aide affiche les deux.

## Mise à jour du firmware

La table `C47.Core/c47_items.tsv` vient des sources C47. Pour la refaire :

```bash
python C47/tools/gen_items.py G:/c47/c43
python C47/tools/gen_help.py G:/c47/c43
python C47/tools/translate_help.py
```

Une description nouvelle ou modifiée reste en anglais : `translate_help.py` la liste dans `tools/help_fr_manquant.txt` ;
il suffit d'ajouter sa traduction à `tools/help_fr.tsv`.

`gen_help.py` range chaque instruction dans un domaine (finances, complexes, constantes…) d'après les menus de la
calculatrice qui la contiennent, puis d'après son nom interne pour celles des menus dynamiques.

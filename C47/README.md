# C47 : encodeur et décodeur de programmes .p47

Bibliothèque `C47.Core` et outil en ligne de commande `c47`, sans dépendance externe.

```bash
dotnet run --project C47.Cli -- enc programme.txt programme.p47
dotnet run --project C47.Cli -- dec programme.p47
dotnet run --project C47.Cli -- test G:/c47/c43
```

## Syntaxe

La syntaxe suit les listings `.p47u` de `rejig` et les exports `XPORTP` :

- `LBL ‘NOM’`, `STO R01`, `RCL R.02`, `RCL →R05`, `XEQ IND 05`, `FS? F01`, `TONE 08` ; plusieurs pas possibles par ligne (`5 3 +`).
  Le décodeur écrit comme les listings `.p47u` récents (`R07`, `F01`, deux chiffres au moins) ; l'encodeur accepte aussi
  la forme de l'écran de la calculatrice (`STO 07`, `FS? 01`, `TONE 8`).
- Nombres : `5` (entier long), `1.5e3` (réel), `3₁₆` ou `111#2` (entier court), `1.5°` (angle). Comme le firmware actuel,
  `₁₆` range un entier de 8 caractères ou plus en binaire ; `#16` le garde en texte, comme les programmes plus anciens.
- Chaîne : `‘TEXTE’`, `"TEXTE"` ou `'TEXTE'`.
- `# texte` reste dans le programme (pas `REM`, que le décodeur écrit aussi `# texte`) ; `@ texte` et `; texte` sont ignorés.
- Les noms de `items.c` (`x≠ ?`) et les noms courants (`X≠0?`, `X<>Y`, `SQRT`) sont acceptés : voir `C47.Core/c47_alias.tsv`.
- Les glyphes propres à la police C47 s'écrivent en clair : indices et exposants sans caractère Unicode en `_abc` / `^abc`
  (`kⓑ` → `k_B`, `STOⓜ` → `STO_M`), `⧰` en `yˣ`, sans les espaces fines (`iℝ`, `RWDOT⋅`). Quand `c47_alias.tsv`
  donne l'orthographe des listings `.p47u`, elle l'emporte (`Draw_y^x`, `FORyˣ`, `∫^x_y`, `CLLCDxy`).
  Le décodeur et l'aide utilisent ces noms ; les codes de la police restent acceptés.
- Écriture au clavier : `x<>y`, `->R05` ou `-> R05`, `X^2`, `Y^X`, `1/X`, `STO*`, `STO/`, `X<=Y?`, `X!=0?`, `R^`, `Rv`
  donnent `x⇄y`, `→R05`, `x²`, `yˣ`, `1/x`, `STO×`, `STO÷`, `x≤? Y`, `x≠? 0.`, `R↑`, `R↓` ; la casse est ignorée quand une
  seule instruction correspond (`abs`, `sqrt`, `lastx`). L'autocomplétion de l'éditeur reconnaît aussi ces écritures.
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

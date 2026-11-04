# DM42 / C47 Editor

Éditeur Windows de programmes pour calculatrices **SwissMicros DM42**, **HP-42S**, les émulateurs **Free42 / Plus42**, et pour la **C47 / R47**.

![Éditeur de programme RPN](docs/editeur.png)

## Fonctions

- **Édition RPN** avec coloration (fonctions, nombres, chaînes, tests, commentaires), numéros de ligne, repères, rechercher / remplacer, impression, thème clair ou sombre.
- **Encodage / décodage** des fichiers `.raw` de la DM42, avec un visualiseur octet par octet (ligne, code RPN, commentaire, octets, erreur).
- **Logigramme** à la manière des feuilles de programmation HP-41C : losanges pour les tests (O / N), blocs d'instructions, appels `XEQ` à double barre, entrées / sorties en parallélogramme, colonne de commentaires. Mis à jour pendant la saisie.
  Il dessine aussi le code C47 (Ctrl+L sur un onglet C47) : structures `IF` / `ELSE` / `ENDIF`, `DO` / `WHILE` / `ENDDO`,
  `REPEAT` / `UNTIL`, `FOR` / `NEXT` (test en bas ou en tête de boucle), tests C47, sauts relatifs `SKIP` / `BACK`, `FINISH`.
- **Simulateur sous l'éditeur RPN** (F5, voir [Simulateur du programme](#simulateur-du-programme)) : simulateur DM42 avec
  pas à pas et points d'arrêt sur un onglet DM42, vrai simulateur C47 avec l'écran de la calculatrice sur un onglet C47.
- **Conversion HP-41C → DM42** de la syntaxe (registres de pile, arguments à deux chiffres, etc.).
- **Python → RPN** (Py42) : écrire le programme en Python, obtenir le listing HP-42S et le simuler avant de l'envoyer.
  Avec la cible **C47 / R47** (liste « Cible » de la fenêtre Python), le programme est produit pour la C47 :
  structures `IF` / `DO` / `FORᵀᴼᴾ` au lieu des `GTO`, variables locales dans les registres `LocR` (récursivité permise),
  noms HP-42S convertis (`42α`, `42ARCL`, `42AVIEW`, `42KEY`…). F5 l'exécute dans le simulateur C47, F6 l'ouvre dans un
  onglet C47, F8 l'enregistre en .p47, F7 l'envoie à l'émulateur C47.
  Exemples C47 / R47 → Programmes de la distribution C47 : douze programmes du dossier `PROGRAMS` du simulateur
  (BMI, NQueens, PAYCALC, FreeF, Bairsto, STRUCT…) réécrits en Python.
- **RPN C47** : des onglets C47 dans le même éditeur (Fichier > Nouveau programme C47, ou ouverture d'un `.p47` / `.p47u`) ; coloration, erreurs signalées pendant la saisie, liste des instructions par domaine (aussi dans Aide > Instructions C47), « Encoder et enregistrer en .p47 » et visualisation des octets (voir [C47/README.md](C47/README.md)).
- **Aide à la saisie** : infobulle sur le mot survolé (instruction, registre `R07`, indicateur `F01`, nombre, remarque…),
  autocomplétion dès deux lettres avec une courte description, et recherche par nom ou description au-dessus de la liste des fonctions.
  Pour la C47 : barre de symboles (→ ⇄ ≤ ≥ ≠ × ÷ ² ˣ √ π…), remplacement pendant la frappe (`->` → `→`, `<>` → `⇄`, `<=` → `≤`,
  `^2` → `²`, sauf dans les textes et commentaires), et écritures au clavier acceptées (`x<>y`, `abs`, `STO*`).
- **Envoi vers l'émulateur** Free42 / Plus42 (F7), dont le chemin se règle dans les paramètres.
- Aide intégrée : liste des fonctions HP-42S et documentation PDF dans une visionneuse WebView2.

| Logigramme | Python → RPN |
|---|---|
| ![Logigramme](docs/logigramme.png) | ![Fenêtre Python](docs/python.png) |

## Simulateur du programme

Fichier → **Simulateur du programme** (Ctrl+F5) affiche ou masque, sous l'éditeur, le même simulateur que la fenêtre Python ;
**F5** (Fichier → Lancer la simulation) l'ouvre et lance le programme. On choisit le label global à lancer, les valeurs
mises sur la **pile** (la dernière va dans X) et les réponses du champ **INPUT**, dans l'ordre : `INPUT` / `PROMPT`,
une valeur par variable d'un `VARMENU`, le numéro de touche d'un `MENU` (1 à 6, 0 = EXIT), le code de touche de
`GETKEY` et `KEY?`. En changeant d'onglet, le simulateur reprend les labels et le type (DM42 ou C47) du nouvel onglet.

### Onglet DM42

Le listing (numéros de pas, en-tête `{ … }` et commentaires ignorés) est exécuté par le simulateur de Py42, en double
précision (16 chiffres, au lieu de 34 sur la DM42). Il connaît, en plus du code produit par Py42 :

- la pile (`ENTER`, `R↑`, `X<>Y`…), les registres numérotés (`RCL 05`, `STO+ 12`), `ISG` / `DSE`, `GTO "NOM"`, `XEQ` local ;
- l'**adressage indirect** `IND` (`RCL IND 05`, `STO IND "N"`, `GTO IND 00`, `XEQ IND "F"`, `ISG IND 00`, `FIX IND ST X`…)
  et les **registres de pile** `ST X` à `ST T` (`RCL ST Y`, `STO+ ST Z`, `X<> ST T`, `VIEW ST Y`) ;
- le **solveur** `PGMSLV` / `SOLVE` : estimations dans la variable et dans X, comme sur la calculatrice ; X = racine,
  Y = estimation précédente, Z = f(racine), T = code (0 racine, 1 changement de signe, 2 extremum, 3 mauvaises
  estimations, 4 constante) ; sans racine, la ligne suivante est sautée ;
- l'**intégration** `PGMINT` / `INTEG` de `LLIM` à `ULIM` avec la précision `ACC` : X = intégrale, Y = incertitude.

Une erreur place le curseur sur sa ligne. `ST L` (LASTX) n'est pas simulé.

**Pas à pas** (touches de Visual Studio, aussi dans Fichier → Pas à pas et dans la barre du simulateur) :

| Touche | Action |
|---|---|
| F9 | Pose ou retire un point d'arrêt sur la ligne du curseur (pastille rouge dans la marge) ; Ctrl+Maj+F9 les efface tous |
| F5 | Lance le programme jusqu'au premier point d'arrêt ; pendant le pas à pas, continue jusqu'au suivant ou à la fin |
| F11 | Pas détaillé : instruction suivante, en entrant dans les `XEQ` et dans la fonction de `SOLVE` / `INTEG` |
| F10 | Pas principal : un `XEQ`, `SOLVE` ou `INTEG` est exécuté d'un coup |
| Maj+F11 | Sortir : jusqu'au retour (`RTN`) du sous-programme en cours |
| Ctrl+F10 | Pas précédent : revient d'une instruction |
| Maj+F5 | Quitte le pas à pas |

La prochaine instruction est surlignée dans l'éditeur, avec une flèche dans la marge ; le simulateur montre la pile T Z Y X,
ALPHA, les variables, les registres, les messages déjà affichés et le niveau d'appel. Le programme est exécuté une fois en
enregistrant l'état avant chaque instruction : les saisies étant données à l'avance, c'est la même chose qu'une exécution
instruction par instruction, et l'on peut revenir en arrière. L'enregistrement s'arrête après 200 000 pas ; un programme
modifié est relancé depuis le début. Les matrices y sont montrées par leurs dimensions.

### Onglet C47

Le programme est encodé puis exécuté par le vrai simulateur C47, sans fenêtre (`c47.exe --headless`), sur 34 chiffres.
Le simulateur montre les messages affichés, la pile de 8 niveaux (exposants lisibles : `TEXTE²`, `×10⁻³`) et
l'écran de la calculatrice (un clic l'agrandit). Pendant la simulation, la fenêtre reste utilisable ; F5, Maj+F5 ou le
bouton Arrêter l'interrompent, et elle s'arrête d'elle-même après 30 s.

Le programme enregistré ou envoyé n'est pas modifié : c'est une copie qui est simulée, préparée pour ce simulateur sans
clavier (`C47/C47.Core/SimCopy.cs`) :

- les messages de `42AVIEW`, `42PRMPT` et `VIEW` sont gardés dans un journal, relu à la fin ;
- un `MENU` suivi de `STOP` demande le numéro de touche, un `VARMENU` une valeur par variable ;
- l'attente d'une touche (`KEY? r`, `GETKEY`) capture l'écran dessiné par le programme, puis lit le code de touche dans
  le champ INPUT ; sans réponse, le programme s'arrête là au lieu d'attendre sans fin (MOON47) ;
- une `PAUSE` juste avant la fin (`PAUSE 99`, « regardez l'écran ») capture l'écran (GRFNTS) ; les autres `PAUSE` sont retirées.

Le pas à pas n'existe que pour la DM42 : le simulateur C47 s'exécute d'un bloc. Seule la première capture d'écran est gardée.

## Compiler

Prérequis : Windows, [SDK .NET 10](https://dotnet.microsoft.com/download), runtime WebView2 (fourni avec Windows 11).

Le projet s'appuie sur deux dépôts voisins, attendus dans le même dossier parent :

```
dm42_editor/
├── dm42editor/   ← ce dépôt
├── Py42/         ← compilateur Python → RPN (Py42.Core)
└── dm42dll/      ← encodeur / décodeur .raw (.NET Framework 4.7.2)
```

1. Cloner les trois dépôts côte à côte (`Py42` avec une majuscule, nom attendu par le projet) :

   ```bash
   mkdir dm42_editor && cd dm42_editor
   git clone https://github.com/philgerm57/DM42Editor.git dm42editor
   git clone https://github.com/philgerm57/py42.git Py42
   git clone https://github.com/philgerm57/dm42dll.git dm42dll
   ```

2. Compiler **d'abord dm42dll** : dm42editor utilise `..\dm42dll\bin\Debug\dm42dll.dll`.
   Il faut le [pack de développement .NET Framework 4.7.2](https://dotnet.microsoft.com/download/dotnet-framework/net472) (ou Visual Studio avec la charge de travail .NET desktop).

   ```bash
   dotnet build dm42dll/dm42dll.csproj -c Debug
   ```

3. Compiler l'éditeur (Py42.Core est compilé au passage) :

   ```bash
   dotnet build dm42editor/dm42editor.slnx -c Release
   ```

Les manuels PDF (DM42, HP-42S) ne sont pas fournis : téléchargez-les sur les sites de SwissMicros et de HP,
puis placez-les dans `dm42editor/data/` (`dm42_user_manual.pdf`, `42s manuel utilisation.pdf`, `Quick Reference Guide.pdf`).

Un profil de publication en fichier unique est fourni : `dotnet publish -p:PublishProfile=FolderProfile`.

## Langue

L'interface existe en français et en anglais : Fichier → Paramètres → Langue (par défaut, la langue de Windows). Le changement s'applique au démarrage suivant. Les traductions sont dans `resources/lang_en.txt`, sous la forme `français TAB anglais`.

## Licence

GNU GPLv3. L'encodeur / décodeur `dm42dll` est une adaptation en C# du code TypeScript de J. Heilingbrunner
([vscode-hp42s-Free42](https://github.com/Heilingbrunner/vscode-hp42s)), sous licence MIT (voir `LICENCE.txt` de dm42dll).

HP-42S est une marque de HP Inc. ; DM42 est un produit SwissMicros. Ce projet n'est affilié à aucune de ces sociétés.

# Spécification des presets d’enregistrement SDR

Statut : profil SDR CQP implémenté dans Media Recorder, Direct3DVideoEncoder et FFmpegMediaWriter ; build Windows reconstruit. Validation locale réalisée sur RTX 5060 pour les 12 combinaisons H.264 Full HD / 4K, 30 / 60 FPS et Low / Medium / High, ainsi que des dimensions supplémentaires. Les limites de validation sont précisées ci-dessous.

## Objectif et périmètre

Media Recorder reçoit un niveau `Low`, `Medium` ou `High`, puis calcule tous les paramètres internes vidéo et audio. `High` est le niveau par défaut. Aucun preset NVENC P1–P7, QP ou bitrate manuel n’est exposé dans cette interface de qualité.

Le profil s’inspire de l’enregistrement simple OBS NVENC. Il ne cherche pas à reproduire les débits de ShadowPlay. Le HDR n’est pas pris en charge dans cette version : une demande HDR doit être refusée explicitement, sans encoder silencieusement une source HDR comme SDR. La source prise en charge est SDR.

Les paramètres de résolution, FPS, source et chemin de sortie restent des entrées indépendantes de la qualité. La qualité ne réduit pas automatiquement résolution ou FPS. L’enregistrement conserve une vidéo continue pendant les changements de caméra.

## Mapping des 3 niveaux

| Paramètre | Low | Medium | High |
|---|---|---|---|
| Codec vidéo par défaut | H.264 NVENC | H.264 NVENC | H.264 NVENC |
| Profil H.264 | High | High | High |
| Profondeur / chroma | 8 bits / 4:2:0 | 8 bits / 4:2:0 | 8 bits / 4:2:0 |
| Régulation | CQP | CQP | CQP |
| QP de base | 27 | 23 | 16 |
| Preset NVENC interne | P5 | P5 | P5 |
| Tuning | High Quality | High Quality | High Quality |
| Multipass | désactivé dans le profil initial | désactivé | désactivé |
| Lookahead | 8 images si supporté, sinon 0 | idem | idem |
| AQ | spatiale force 8 ; temporelle si supportée | idem | idem |
| B-frames | jusqu’à 2 selon capacité maximale du GPU | idem | idem |
| Intervalle clés | 250 images, IDR tous les 250 | idem | idem |
| Codec audio | AAC | AAC | AAC |
| Débit audio | 128 kbit/s | 192 kbit/s | 192 kbit/s |
| Fréquence audio | 48000 Hz | 48000 Hz | 48000 Hz |
| Canaux audio | 2, stéréo | 2, stéréo | 2, stéréo |
| Couleurs de sortie SDR | BT.709, plage limitée | idem | idem |

Un QP plus élevé accepte davantage de perte de détail. Le preset P5 contrôle l’effort de l’encodeur ; il est commun aux niveaux et n’est pas assimilé à la qualité utilisateur.

Les bases 23 et 16 viennent de la logique OBS. Low = 27 est notre choix implémenté. Les débits audio sont notre mapping, pas une correspondance officielle des niveaux OBS. Le multipass désactivé est un choix simplificateur de cette spécification : OBS déclare un défaut quarter-resolution, dont l’effet final en CQP n’a pas encore été établi ici. Toute activation ultérieure exige une validation et une révision de la spécification.

## Qualité selon la résolution

**Exigence : aucune liste fermée de résolutions.** Le calcul accepte toute largeur et toute hauteur valides pour le codec et le GPU : formats 16:9, ultrawide, portrait et dimensions personnalisées. 1080p et 4K sont des exemples de validation, pas des branches de sélection hardcodées.

Valider les limites de dimensions du backend et les contraintes d’alignement du format 4:2:0. Dans cette version, les dimensions impaires sont refusées explicitement plutôt que redimensionnées silencieusement. Une dimension valide n’est pas refusée uniquement parce qu’elle est absente des exemples.


Pour rester proche de la correction du mode simple OBS, calculer :

`réduction = floor((1 - min(2000, sqrt(width² + height²)) / 2000) × 10)`

`QP effectif = clamp(QP de base - réduction, 1, 51)`

Le calcul doit utiliser des nombres flottants ou des entiers suffisamment larges pour éviter un débordement lors du calcul de la diagonale. La résolution doit être strictement positive. Les FPS doivent également être strictement positifs et supportés par le backend.

En 1920×1080 et 3840×2160, la réduction vaut 0 : QP effectifs 27 / 23 / 16, à 30 ou 60 FPS. Aucune multiplication du bitrate par le nombre de pixels ou par les FPS n’est nécessaire en CQP. Le budget produit varie naturellement avec résolution, cadence, mouvements et complexité de la scène.

## Paramètres de débit

CQP ne comporte pas de bitrate vidéo cible ni de maximum imposé dans ce profil. Le CQ de VBR n’est pas configuré : CQP et VBR avec qualité cible sont des régulations distinctes.

Le backend doit configurer les QP constants des images I, P et B d’après le QP effectif, et vérifier par télémétrie les valeurs réellement soumises. Pour le profil initial, les 3 QP sont identiques ; cette règle est notre choix simple, pas une affirmation sur tous les paramètres finaux d’OBS.

Aucune garantie de taille maximale, de bitrate constant ou de rapport de compression fixe n’est fournie. Les débits réellement mesurés doivent être conservés dans les statistiques.

## Capacités et configuration native

Le backend interroge les capacités du GPU pour le lookahead, l’AQ temporelle et les B-frames. L’AQ spatiale est demandée à force 8, sans interrogation de capacité dédiée. Une capacité facultative absente entraîne la désactivation de l’option et un diagnostic. Un codec ou preset nécessaire non supporté entraîne une erreur explicite ; ne pas basculer silencieusement vers un autre profil.

### Configuration native arrêtée

Ces valeurs communes sont appliquées explicitement après récupération de la configuration native P5 / HQ, afin d’éviter des valeurs cachées héritées du preset :

| Champ / décision | Valeur retenue |
|---|---|
| `rateControlMode` | `NV_ENC_PARAMS_RC_CONSTQP` |
| `constQP.qpIntra / qpInterP / qpInterB` | QP effectif calculé, identique pour les 3 types |
| `averageBitRate / maxBitRate / vbvBufferSize / vbvInitialDelay` | 0 ; aucun budget vidéo imposé |
| `targetQuality / targetQualityLSB` | 0 ; non utilisés en CQP |
| `enableAQ / aqStrength` | 1 / 8 |
| `enableTemporalAQ` | 1 si `NV_ENC_CAPS_SUPPORT_TEMPORAL_AQ`, sinon 0 |
| `enableLookahead / lookaheadDepth` | 1 / 8 si `NV_ENC_CAPS_SUPPORT_LOOKAHEAD`, sinon 0 / 0 |
| `disableIadapt / disableBadapt` | 0 ; adaptation autorisée |
| `gopLength / h264Config.idrPeriod` | 250 / 250 |
| B-frames effectives | `min(2, capacité maximale annoncée)` |
| `frameIntervalP` | B-frames effectives + 1 |
| Référence B-frame | désactivée |
| `multiPass` | `NV_ENC_MULTI_PASS_DISABLED` |
| `enableInitialRCQP / enableMinQP / enableMaxQP` | 0 ; ne pas ajouter une autre contrainte de QP |
| Métadonnées couleur H.264 | signal et description présents ; primaires / transfert / matrice = 1 / 1 / 1 ; plage complète = 0 |
| Remplissage CBR | désactivé |
| SPS/PPS aux IDR | répétés ; choix d’intégration de notre pipeline |

OBS définit le GOP automatique à 250 images, active l’AQ spatiale avec force 8 et l’AQ temporelle selon capacité. Son lookahead peut reprendre une profondeur du preset ou utiliser 8. Nous fixons explicitement 8 pour une configuration simple et déterministe, donc sans promettre une identité totale avec OBS.

Un GOP de 250 dure environ 8.33 s à 30 FPS et 4.17 s à 60 FPS. Le passage d’une caméra à l’autre ne force pas une IDR et ne réinitialise pas le GOP.

Le backend réserve 16 surfaces dans le chemin CQP. Avec lookahead ≤ 8 et B-frames ≤ 2, cela couvre `frameIntervalP + lookaheadDepth + 5`. Il accepte `NEED_MORE_INPUT`, transmet EOS avant la vidange, puis récupère les sorties différées. Les paquets peuvent arriver en ordre de décodage ; leurs timestamps de présentation viennent de `outputTimeStamp`. FFmpegMediaWriter transmet PTS/DTS dans MPEG-TS, puis copie la vidéo dans MKV et MP4 sans réencodage.

Si `nvEncInitializeEncoder` renvoie `NV_ENC_ERR_INVALID_PARAM` avec une option AQ activée, le code autorise 1 nouvelle tentative sans AQ spatiale ni temporelle. Ce statut ne prouve pas que l’AQ était la cause ; le diagnostic indique le repli et les options effectives. Les autres statuts ne déclenchent pas ce retry, et une seconde erreur est propagée.

Source vérifiée pour le GOP, le lookahead, l’AQ et les QP : [initialisation native OBS NVENC](https://raw.githubusercontent.com/obsproject/obs-studio/master/plugins/obs-nvenc/nvenc.c). Les zéros des champs de débit, le multipass désactivé et la répétition SPS/PPS sont nos décisions d’intégration, et non une copie exacte de tous les defaults OBS.

H.264 SDR est le codec par défaut et celui validé par la matrice locale. HEVC SDR reste accessible : il applique la même politique CQP, avec profil Main / 8 bits et capacités interrogées pour HEVC. Les 3 niveaux HEVC SDR sont vérifiés localement en 4K à 60 FPS, avec une capture High de 120 s sous charge. Les autres combinaisons HEVC ne bénéficient pas de la matrice complète H.264 ; aucun support HDR n’est fourni.

## Contrat et diagnostics

- Niveau obligatoire dans le modèle, valeur par défaut High ; enum inconnu refusé.
- Construction d’un profil immuable depuis niveau et dimensions, avec validation des FPS ; le backend natif applique ensuite les capacités GPU.
- Pas de réglage utilisateur P4 / P5, QP, AQ ou multipass.
- Statistiques : niveau demandé, codec, dimensions, FPS, QP I/P/B, preset, tuning, régulation, options AQ/lookahead, B-frames, GOP effectif, paramètres audio, débit mesuré et pertes de frames.
- Les diagnostics doivent distinguer paramètres demandés et paramètres effectivement soumis au backend.

## Validation et limites

Validation locale réalisée : les 3 niveaux en 1080p et 4K à 30 et 60 FPS, paramètres natifs, H.264 High BT.709 limité, AAC 48000 Hz stéréo, nombre d’images conservées et durées audio/vidéo. Sur ces séquences, la taille augmente de Low vers Medium puis High.

Cas supplémentaires réalisés : 1280×720, 2560×1440, 3440×1440, 1080×1920, 2048×1152, mode NVENC synchrone et cycle des 3 caméras pendant 13 s. Le dernier cycle se termine sans erreur EOS ; Screen conserve l’UI à l’endroit. Les tests du calcul et de l’export natif rejettent les dimensions nulles/impaires, FPS nul, enum/codec inconnu et QP invalide. Le transport teste les PTS/DTS en ordre de décodage.

Restent à vérifier : options facultatives indisponibles et retry sans AQ sur un matériel adapté, rejet effectif au-delà des capacités GPU, qualité perceptuelle exhaustive et autres combinaisons de résolution/FPS HEVC. Le refus `CaptureHdr = true` est explicite dans le code ; aucune source HDR réelle n’a été testée. Ces limites n’empêchent pas l’utilisation du profil SDR validé localement et ne constituent pas une garantie sur tous les GPU.

## Sources et état actuel

- [OBS : sélection de la qualité en mode simple et correction de résolution](https://raw.githubusercontent.com/obsproject/obs-studio/master/frontend/utility/SimpleOutput.cpp).
- [OBS : defaults et capacités du plugin NVENC](https://raw.githubusercontent.com/obsproject/obs-studio/master/plugins/obs-nvenc/nvenc-properties.c).
- [NVIDIA : guide officiel de programmation NVENC](https://docs.nvidia.com/video-technologies/video-codec-sdk/13.1/nvenc-video-encoder-api-prog-guide/index.html).

Le chemin de qualité Media Recorder utilise CQP P5/HQ et AAC selon le niveau. Les anciens exports natifs VBR restent disponibles pour compatibilité ; ils ne sont pas appelés par ces profils. High et H.264 sont les valeurs par défaut. UnitySample sélectionne et teste les profils, avec 1 vidéo et alternance des caméras toutes les 4 s jusqu’à l’arrêt demandé.

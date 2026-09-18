# Validation des presets SDR CQP

Tests locaux sur RTX 5060, Direct3D 11 / NVENC, 2026-09-19. Scène mobile UnitySample, 4 s demandées par test, son actif. Les images de préparation et de vidange expliquent les durées légèrement supérieures. Même politique vidéo pour les 3 niveaux ; QP et débit AAC varient.

Contrôles automatiques : télémétrie sans erreur, nombre d’images soumises = terminées = décodées dans le MP4, H.264 High SDR BT.709 plage limitée, AAC 48000 Hz stéréo, profil natif P5/HQ CQP, GOP 250, B-frames 2, lookahead 8. Les débits ci-dessous sont mesurés dans les fichiers, et ne sont pas des cibles CQP. L’écart A/V est la différence des durées, pas une mesure perceptuelle de synchronisation.

| Résolution | FPS demandés | Qualité | QP | Images décodées | Pertes natives | Taille Mo | Vidéo Mbit/s mesurés | Écart durées A/V ms |
|---|---:|---|---:|---:|---:|---:|---:|---:|
| 3840×2160 | 30 | High | 16 | 135 | 0 | 58.34 | 103.86 | 5.0 |
| 3840×2160 | 30 | Low | 27 | 134 | 0 | 17.90 | 31.96 | 2.3 |
| 3840×2160 | 30 | Medium | 23 | 135 | 0 | 28.38 | 50.37 | 12.3 |
| 3840×2160 | 60 | High | 16 | 259 | 0 | 92.25 | 170.98 | 1.7 |
| 3840×2160 | 60 | Low | 27 | 258 | 0 | 27.09 | 50.26 | 11.0 |
| 3840×2160 | 60 | Medium | 23 | 258 | 0 | 44.84 | 83.25 | 10.0 |
| 1920×1080 | 30 | High | 16 | 133 | 0 | 22.73 | 40.80 | 4.3 |
| 1920×1080 | 30 | Low | 27 | 133 | 0 | 7.62 | 13.65 | 1.0 |
| 1920×1080 | 30 | Medium | 23 | 133 | 0 | 11.80 | 21.08 | 5.3 |
| 1920×1080 | 60 | High | 16 | 251 | 0 | 36.86 | 69.20 | 2.7 |
| 1920×1080 | 60 | Low | 27 | 254 | 0 | 11.33 | 21.25 | 7.3 |
| 1920×1080 | 60 | Medium | 23 | 254 | 0 | 18.32 | 34.47 | 3.0 |

Les tests du calcul de qualité couvrent également 720p, 1440p, 3440×1440, portrait et résolution personnalisée, ainsi que les dimensions impaires/nulles, FPS nul et enum inconnu. Le transport teste explicitement une arrivée des images en ordre de décodage 0/3/1/2 et vérifie PTS, DTS, alignement TS et conservation du payload. Commande : `dotnet run --project UnityMediaRecorder/Tests/QualityProfileTests.csproj -c Release` depuis le dossier parent des projets.

Un premier enregistrement de 13 s vérifie les passages Camera 1 / Camera 2 / Screen et le retour à Camera 1. La vue Screen a été inspectée visuellement et contient l’UI à l’endroit ; le fichier se décode sans erreur. Il a révélé un besoin d’événement EOS dédié, corrigé avant les tests du tableau.

Limites : ces tests n’établissent pas une identité avec ShadowPlay ni une validation perceptuelle exhaustive. Les replis de capacités et le retry sans AQ ne sont pas exercés sur ce GPU, qui supporte toutes les options. Le HDR est explicitement refusé. Les tests complémentaires sur dimensions personnalisées et HEVC sont consignés ci-dessous lorsqu’ils sont réalisés.

## Contrôles complémentaires du build final

Horodatages DTS strictement croissants, PTS ≥ DTS, dimensions réelles et nombre de paquets vérifiés ; toutes les images soumises ont été conservées.

| Cas | Résolution | QP | Mode natif | Images conservées | Pertes |
|---|---|---:|---|---:|---:|
| 1440p | 2560×1440 | 23 | async | 74 | 0 |
| 720p | 1280×720 | 21 | async | 73 | 0 |
| custom | 2048×1152 | 23 | async | 73 | 0 |
| portrait | 1080×1920 | 23 | async | 73 | 0 |
| sync | 1280×720 | 21 | sync | 77 | 0 |
| ultrawide | 3440×1440 | 23 | async | 74 | 0 |
| cycle final 13 s | 1920×1080 | 16 | async | 795 | 0 |

L’export natif rejette aussi dimensions nulles/impaires, FPS nul, codec inconnu et QP 0/52. Le cycle final de 13 s se termine sans erreur EOS ; le profil High et l’AAC 192 kbit/s restent les valeurs par défaut. Le contrat HDR est refusé explicitement dans StartRecording, mais aucun test de source HDR réelle n’est possible sur l’écran SDR local. Les capacités facultatives manquantes et le repli AQ nécessitent un autre GPU ou un test ciblé du driver.

## Validation HEVC SDR et capture longue

Tests sur la même RTX 5060 : 3840×2160, plafond 60 FPS, Low / Medium / High pendant 13 s avec les 3 caméras. Le test long High dure 120 s demandées, avec rendu 4K, MSAA 8× et VSync désactivée. L’encodeur reste ouvert pendant les changements de source.

| Test | QP | Images dans le MP4 | Pertes natives | FPS réels | Écart durées A/V ms | Taille Mo |
|---|---:|---:|---:|---:|---:|---:|
| High 13 s | 16 | 801 | 0 | 59.92 | 12.33 | 154.70 |
| Low 13 s | 27 | 780 | 0 | 58.58 | 3.00 | 50.45 |
| Medium 13 s | 23 | 798 | 0 | 59.94 | 2.00 | 77.91 |
| Long High 120 s | 16 | 6970 | 0 | 57.91 | 4.33 | 1269.32 |

Contrôles : HEVC Main 8 bits / 4:2:0 SDR, BT.709 limité, CQP P5/HQ, GOP 250, B-frames 2, lookahead 8 et AQ effectifs. Audio AAC 48000 Hz stéréo, cibles 128 / 192 / 192 kbit/s. Le nombre de paquets vidéo dans chaque MP4 correspond aux images soumises et terminées. DTS vidéo strictement croissants, PTS ≥ DTS et audio sans trou dans les horodatages de paquets. La vue Screen est inspectée à 9 s ; elle contient l’UI à l’endroit.

La cadence réelle d’environ 58 FPS sous charge n’est pas une perte native : le rendu ne fournit pas toujours 60 images par seconde. Les captures conservent leurs timestamps de présentation. L’écart de durées A/V est faible, mais ne constitue pas une preuve perceptuelle de synchronisation : aucun repère audiovisuel dédié n’est présent dans la scène. La matrice HEVC complète à d’autres résolutions/FPS et les replis sur d’autres GPU restent à vérifier. H.264 reste le codec par défaut ; HDR reste non supporté.

Mesures détaillées : [hevc-recording-validation.json](hevc-recording-validation.json). Les MKV PCM intermédiaires sont conservés pour contrôle audio. Aucun réglage d’encodeur ni build n’a été changé par ces tests.

Les 4 fichiers HEVC (3 profils + test long) se décodent intégralement avec FFmpeg/NVDEC. Le contrôle conserve la timebase du fichier pour éviter un arrondi artificiel à 60 FPS dans le muxeur de test. La vue Screen du test long est également vérifiée à 105 s, UI à l’endroit.

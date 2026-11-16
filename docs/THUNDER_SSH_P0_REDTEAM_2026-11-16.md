# Technisches Red Team: Thunder SSH-P0 und vollständige Bereitstellung

**Datum:** 16.11.2026. **Scope:** ausschließlich Thunder Compute Desktop Client (`feature/windows-desktop-client`). **Quellen:** vom Tester gelieferte Screenshots und minutengenaue GUI-/Deployment-Logs; offizielle Thunder-Console-Anleitungen und dokumentierte Thunder REST-API; offizieller Open-Source-`tnr`-Client; geprüfter Clientcode. **Verbotene Maßnahmen:** keine kostenpflichtige neue GPU-Instanz, kein Löschen, kein unbestätigter Fingerprint, keine Bearbeitung des privaten Upstream-Repositories.

**Freigabestatus:** P0 **nur code- und CI-seitig entschärft**, **kein echter erfolgreicher Provider-SSH-Login nachgewiesen**. Kein Stable-Release und kein unbeaufsichtigter bezahlter GPU-Test.

## 1. Tatsächlich beobachtete Evidenz (nicht Interpretation)

Tester-Screenshot vom 16.11.2026, 15:43 Uhr: privater GitHub-Katalog geladen (18 Workflows, eine Auswahl), automatischer GPU-Fallback A6000 → L40, ein Pod mit ID `0` erstellt, Status `STARTING` und anschließend SSH-IP/Port verfügbar. Danach `tnr connect --json` beziehungsweise `POST /v1/instances/0/add_key` mit ca. 30 Sekunden API-Frist abgebrochen. GUI zeigt `Thunder konnte den SSH-Schlüssel nicht rechtzeitig einrichten`; ComfyUI-Bootstrap nicht gestartet; keine Downloadereignisse.

**Belegt:** Preisprüfung, GPU-Kapazitäts-Fallback und Pod-Anlage funktionieren in dieser einen realen Episode. **Nicht belegt:** dass der `add_key`-POST überhaupt unbeantwortet am Server blieb oder serverseitig scheiterte; die Anfrage kann nach erfolgreichem Write vor der Antwort abgebrochen sein. **Ebenfalls nicht belegt:** der aktuelle Pod-Status außerhalb des Screenshot-Zeitpunkts. Ein Pod mit Status `RUNNING` kann weiter kosten; `0.805 USD/h` ist eine **Schätzung, kein Billing-Limit**.

Ältere Startdiagnose aus der eingesandten zweiten Datei dokumentiert frühere Windows-`git clone`-Fehler bei Pfadnamen mit Doppelpunkten und `tnr`-PATH-Probleme. Die aktuelle GUI hat dagegen **18 Workflows geladen und einen Pod per `tnr` erstellt**. Diese alten Startprobleme erklären den aktuellen P0 nicht; sie dürfen nicht als dessen vermeintliche Ursache gemeldet werden.

## 2. Offizielle Quellen mit technischer Aussage

1. [Thunder CLI: Connecting to instances](https://www.thundercompute.com/docs/cli/operations/connecting-to-instances): `tnr login`, `tnr status --no-wait`, `tnr connect <id>`; `tnr` richtet den SSH-Zugang automatisch ein. **Nicht** dokumentiert ist eine garantierte einmalige Lieferung einer privaten Key-Datei bei einem verlorenen API-Response.
2. [Thunder CLI: Source cmd/connect.go](https://github.com/Thunder-Compute/thunder-cli/blob/main/cmd/connect.go): ohne lokalen Key ruft die CLI `client.AddSSHKeyCtx(ctx, instanceID)` auf; die API erzeugt mit dem CLI-Parameterlosen Aufruf ein Schlüsselpaar. Die lokale private Hälfte wird erst **nach dem erfolgreichen API-Response** gespeichert. Der `http.Client` hat 30 Sekunden Default-Timeout. Das passt exakt zum gemeldeten Fehlerbild.
3. [Thunder Console: SSH manually](https://www.thundercompute.com/docs/console/operations/ssh): gespeicherte SSH-Keys sind auf Organisationsebene. Bei **Console-Create** kann ein vorhandener öffentlicher Key ausgewählt werden; bei bestehenden Pods muss er gesondert hinzugefügt werden. Die Key-Datei auf dem Windows-PC ist die private Hälfte; Standard-SSH verwendet `ssh ubuntu@IP -p Port -i Pfad`. **Ein im Account gespeicherter Key ist kein Beweis für dessen Autorisierung auf dem bereits laufenden Pod.**
4. [Thunder REST: Add SSH key to instance](https://www.thundercompute.com/docs/api-reference/instances/add-ssh-key-to-instance): `POST /v1/instances/{id}/add_key` mit JSON `{"public_key":"ssh-ed25519 ..."}`; ein **identischer** Public-Key wird ausdrücklich **ohne Fehler als No-op** behandelt. Ohne Feld `public_key` erstellt Thunder selbst ein neues Schlüsselpaar; private Hälfte wird **nur einmal** zurückgegeben. Bearer-Authorization erforderlich.
5. [Thunder REST: List instances](https://www.thundercompute.com/docs/api-reference/instances/list-instances): dokumentierter **lesender** `GET /v1/instances/list` mit Bearer Token; als Vorab-Anmeldetest für GUI geeignet. Ein erfolgreicher GET bestätigt nur den eigenen API-Token-Zugang, nicht automatisch denselben Account/Workspace wie die parallel verwendete `tnr`-Anmeldung.
6. [Thunder Console: Authentication](https://www.thundercompute.com/docs/console/operations/authentication): API-Tokens können in der Console erzeugt/widerrufen werden; eindeutige Tokens pro Anwendung empfohlen. Der Desktop speichert sein Token getrennt von GitHub/HuggingFace/Civitai im Windows Credential Manager.

## 3. Root-Cause-Analyse: Warum die vorherigen Clientversionen weiterhin scheitern

Der ursprüngliche automatische Pfad ruft nach dem kostenpflichtigen `create` unweigerlich `tnr connect --json` auf. Er besitzt kein eigenes persistiertes Paar, **wenn die CLI im lokalen `TNR_HOME/keys/<uuid>` noch keinen privaten Key gespeichert hat**. `tnr connect` ruft `POST /add_key` ohne `public_key` auf. Dieser API-Aufruf ist hinsichtlich der **lokalen privaten Hälfte** nicht wiederholbar: Wenn eine Antwort verloren geht, kann das Public-Key-Material bereits remote vorliegen, während die passende private Hälfte lokal unbekannt ist.

Der erste Fix mit `TryExistingSshKey` hilft **nur, wenn eine nutzbare private Schlüsseldatei bereits lokal existiert**. Er behebt den ersten Pod-Zugang gerade nicht. Die vorliegenden Logs sind deshalb nicht widersprüchlich, sondern ein reproduzierbarer Pfad durch diese Lücke.

**Widerlegte Erklärungen:** GPU-A6000-Mangel (korrekt abgefangen), GPU-L40-Defekt (RUNNING mit SSH-Port), GitHub-Token/Katalog (Katalog erfolgreich), ComfyUI-Start-/Modell-Dateiprobleme (Bootstrap noch gar nicht begonnen), automatisches Löschen/zweiter Create (der Client verweigert es bewusst). **Mögliche externe Auslöser, noch nicht trennbar:** Thunder-API-Latenz, fehlendes Backend-SSH-Key-Write, verzögerte `authorized_keys`-Synchronisierung, Konto-/Workspace-Differenz oder SSH-Host-/Schlüssel-Mismatch.

## 4. Umgesetzte Reihenfolge im Entwicklungsbranch

| Gate | Plausibilitätsprüfung | Sperre / sichere Folgemaßnahme |
|---|---|---|
| G0 Quelle | Privaten Katalog laden, vollständige Model-IDs prüfen, SHA/Adapter pinnen | Bei unvollständiger Auswahl kein Create |
| G1 Thunder CLI | `tnr status --json --no-wait` lesend, vorhandene Pods und ID identifizieren | Bei mehreren Pods kein Auto-Create |
| G2 Thunder REST | Benannten Console-Token über `GET /v1/instances/list` prüfen, keine API-Mutation | Fehlender/ungültiger Token und kein bereits lokal nutzbarer Key ⇒ **kein kostenpflichtiger Auto-Create** |
| G3 Lokaler SSH-Key | Eigene permanente ED25519-Identität vor Create anlegen oder expliziten vorhandenen privaten Key prüfen; `ssh-keygen -y` leitet Public-Key daraus ab; Schlüsseldatei nicht an Provider übertragen | Ungültige Datei, fehlende Rechte, nicht-ED25519, Passphrase-Problem ⇒ **kein Create** |
| G4 Anbieterquote | `GET /v2/pricing` und `/v2/specs`, GPU/vCPU/Disk, Obergrenze und explizite GUI-Bestätigung | Teurer/unklar ⇒ kein Create |
| G5 Create | `tnr create --json`, Identität/UUID aus Create-Antwort, Status-Poll der exakten ID | Kein zweiter Pod bei verlorener Statusliste |
| G6 SSH-Endpunkt | Vom Provider gemeldete IP, **SSH-Port** (nicht hartkodiert 22), `RUNNING`, ID/UUID syntaktisch validieren | Bei falscher IP/Port/Pod-ID keine Key-Mutation |
| G7 Public-Key-Write | `POST /v1/instances/{id}/add_key` mit **immer vorhandenem `public_key`**, HTTPS Bearer, keine Redirects; maximal zwei gleiche Versuche bei Timeout/5xx; `success=true`, UUID, fehlender servergenerierter Private-Key in Antwort prüfen | 401/403/400/UUID-Konflikt: STOP. Bei 5xx/Timeout: Anbieterstatus unklar, **kein `tnr connect`**, nach Hostprüfung nur identischen lokalen Key per SSH testen |
| G8 Hostvertrauen | ED25519-Hostfingerprint unabhängige Thunder-Quelle + separate explizite Zustimmung, `StrictHostKeyChecking=yes` | Ohne Fingerprint: STOP; niemals blind `accept-new` |
| G9 SSH | `ssh -i <privat> -p <providerport> ubuntu@<providerip> true`, `BatchMode=yes`, keine TTY-/Passwortfallbacks | Nur **echter Erfolg** entsperrt nächsten Schritt |
| G10 GPU | SSH `nvidia-smi` VRAM/Anzahl und freier persistenter Workspace | Unzureichende Ressourcen: keine Modelldownloads |
| G11 Bootstrap | Remote-Supervisor, SHA-gepinnter Adapter, Run-ID/PID/Log, keine Secrets in Remote-Skript | Bestehenden Job anhängen, keine zweite Installation |
| G12 ComfyUI | Download-Endereignisse, Node-/Dependency-Audit, private HTTP-API, SSH-Readiness-Audit | HTTP 200 allein **keine** einsatzbereite Meldung |

**Kritischer Hinweis:** G2 ist ein *Authentifizierungs- und Erreichbarkeitstest*, keine kryptografische Kontrolle, dass der `tnr`-Client exakt denselben Org-/Workspace-Kontext verwendet. G7 nutzt eine **dokumentierte** POST-Route; bei echten Backend-Ausfällen kann auch der sichere Public-Key-Modus fehlschlagen. G8 verlangt weiterhin eine außerhalb der vom Host gelieferten Daten geprüfte Vertrauensquelle.

## 5. Adversarial Checks (ohne paid provider)

| Test | Angriff / unerwarteter Zustand | Erwartung |
|---|---|---|
| RT01 | Kein API-Token vor Create | Kein automatischer bezahlter Pod |
| RT02 | API-Token 401/403 | Kein Create, keine Ausgabe des Tokens |
| RT03 | API-Token GET liefert unerwartetes Schema | Kein Create |
| RT04 | Privater Key fehlt/ungültig | Fail-closed vor Create |
| RT05 | Nur .pub-Datei vorhanden | Kein Überschreiben, kein Create |
| RT06 | Eigener Windows-Key, echtes `ssh-keygen -t ed25519` + `-y` | Lokal persistierte private Hälfte, korrekter kommentarloser Public-Key |
| RT07 | API-POST für SSH | Der Body enthält **immer** `public_key`, nie `private_key` |
| RT08 | 503 beim ersten POST | Wiederholung **mit byte-identischem `public_key`**, maximal zweimal |
| RT09 | Zweimal 503 / Timeout | Ambiguous; nur echten SSH-Login mit demselben lokalen Key versuchen; kein `tnr connect` |
| RT10 | HTTP 401 beim POST | Kein Retry und kein SSH-Ready |
| RT11 | Antwort `success=false` / unexpected private `key` | Sperren, Rohantwort nie protokollieren |
| RT12 | Antwort-UUID passt nicht zum Pod | Sperren, keine falsche Pod-Zuordnung |
| RT13 | SSH-Host-Key nicht bestätigt | Keine Remote-Ausführung |
| RT14 | SSH-Endpunkt Port=0 / ungültige IP / Status != RUNNING | Kein Public-Key-POST |
| RT15 | Bestehender Pod mit lokalem Key, kein API-Token | Nur read-only direkter SSH-Zugang, keine Keygenerierung |
| RT16 | Katalog/Preise/GPU-Specs unvollständig | Kein Create |
| RT17 | Remote-Modell/Node fehlt, API HTTP 200 | Keine Fertigmeldung |
| RT18 | Bootstrap/GUI-Verbindung bricht ab | Remote-Job behält Run-ID/Log; kein Zweit-Create, erneutes Anhängen |
| RT19 | API-Key in Exception/HTTP-Response | Keine vertraulichen Werte in Error/Dialog/Log |
| RT20 | Windows-Navigation und neue Readiness-Seite | GUI-Smoke inkl. vollständiger Systemchecks |
| RT21 | Vorherige Checkout-/PATH-Fehler im historischen Startup-Log | Nicht fälschlich als Ursache des aktuellen SSH-Falls klassifizieren |
| RT22 | Nur Account-Key gespeichert, nicht an bestehenden Pod gebunden | Kein „SSH bereit“ allein aufgrund eines Keys im Org-Store |
| RT23 | Client-Token stammt von anderem Thunder-Workspace | Kein irrtümliches „SSH bereit“; **zusätzlicher Live-Org-Test weiterhin erforderlich** |

## 6. Release-/Livetest-Gates

Die Windows-CI prüft .NET10-WPF, Unit-Tests, eigenen echten `ssh-keygen`-Aufruf, GUI-Start, Inno-Setup-Installer. Die Python-Fixtures führen die tatsächliche Remote-Readiness-Audit-Python gegen synthetische Dateien und mockierte lokale ComfyUI-Antworten aus. Der Linux-Smoke testet den tatsächlichen remote-simulierenden Bootstrap-Supervisor inklusive Reattach. **Das ist kein Cloud-End-to-End-Erfolg.**

Vor einer Freigabe in einem ausdrücklich kostenbestätigten Test mit **dem bereits existierenden Pod, nicht durch Neuanlage** erforderlich: G2 API-Token und Org-Zuordnung real bestätigt; G7 dokumentierter Public-Key-POST real bestätigt **oder** bei Timeout G9 echter SSH-Login; G8 Hostfingerprint unabhängig verifiziert; G9 mindestens ein erfolgreicher `ssh true`; G10 GPU-/Disk-Preflight; G11 Downloadlogs; G12 ComfyUI-/Workflow-Fertigprüfung; eine echte Inferenz mit einem der gewählten Modelle. Support-Eskalation, falls bereits G7/G9 scheitern.

Bis zu diesen Nachweisen bleibt die Aussage **„die Software funktioniert vollständig auf Thunder“ nicht belegt**. Die tatsächlich bestätigte Aussage lautet: **der bisher bekannte nicht-idempotente Keygen-Pfad wurde aus der automatischen GUI-Bereitstellung entfernt und durch eine dokumentierte, lokal schlüsselgebundene Route ersetzt.**

## 7. Ergebnis und Betreiberhinweis

Ein vorhandener Pod kann weiterhin Kosten verursachen, auch wenn Download und Bootstrap noch nicht gestartet sind. Keine weitere Instanz aus diagnostischen Gründen anlegen. Vor Tests, die eine bereits vorhandene Instanz betreffen, klare aktive Pod-Auswahl und ausdrückliche Bestätigung; niemals automatisch löschen. Bei nicht erreichbarer SSH-API bereinigte Logs und Instanz-ID an Thunder-Support; niemals private SSH-Keys, Named API Tokens oder GitHub Tokens weitergeben.

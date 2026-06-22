# Client-аас ирүүлсэн 5 асуудлын засварын дэлгэрэнгүй төлөвлөгөө

Огноо: 2026-06-22
Branch: ui-redesign
Аргачлал: systematic-debugging (root cause → fix). Доорх бүх root cause-ийг кодоос баталгаажуулсан.

## ХЭРЭГЖҮҮЛЭЛТИЙН СТАТУС (2026-06-22)
- [x] Үе 0 — ApiService print-status logging + trace (ApiService.cs)
- [x] #1 — `IsSuccessfulUpdatePrintStatusResponse` чангатгал + warning лог (ApiService.cs)
- [x] #2 — RFID-гүй олон тоог нэг `^PQ` job болгосон (LabelPreviewViewModel.cs)
- [x] #3 — Preview баркодыг хэвлэлтийн engine рүү нэгтгэв (shared `LabelRenderEngine.BuildBarcodeImage`;
      хэвлэлтийн гаралт ХЭВЭЭР, scannable; preview одоо print-тэй ижил)
- [ ] #4 — EPC sync (backend лог шаардлагатай — хойшлуулсан)
- [ ] #5 — Server-side filter feature (хойшлуулсан)
- [ ] ДУТУУ: #1 multi-RFID статус (business logic), #2 RFID-замын flow control,
      #3 урт текст fit — бүгд принтер/бодит тест шаардана
Build: 0 error. Бүх засвар нь физик принтер дээр client-ийн баталгаажуулалт хүлээж байна.

---

## Эрэмбэ (хийх дараалал)

1. **Үе 0 — Нотолгооны logging** (#1, #4-ийг батлахад зайлшгүй)
2. **Үе 1 — #2 хэвлэлтийн buffer race** (хамгийн их өвддөг, тодорхой root cause)
3. **Үе 2 — #1 print-status sync**
4. **Үе 3 — #3 баркод/текст багтаах + preview=print нэгтгэл**
5. **Үе 4 — #5 server-side filter (шинэ боломж)**
6. **Үе 5 — #4 EPC sync (backend-тэй хамт)**

---

## Үе 0. Нотолгооны Logging (Diagnostics)

**Зорилго:** #1, #4-ийн бодит шалтгааныг батлах. Одоо `UpdatePrintStatusAsync` зөвхөн `Debug.WriteLine` рүү бичдэг тул production-д ул мөр алга.

Хийх:
- `ApiService.UpdatePrintStatusAsync`-д `ILoggingService`-г inject хийж, request payload + бодит HTTP status + response body-г файлын лог руу бичих.
- `WriteApiTraceAsync`-г UpdatePrintStatus болон бусад POST дуудалтад мөн ашиглах (одоо зөвхөн GetResources-д).
- `PrintAndPushRfid`, `EnqueueBranchSync`, `EnqueueEquipmentSync`-д адил лог нэмэх.

Файл: `BarTenderClone/Services/ApiService.cs`, `IApiService.cs` (DI signature)
Эрсдэл: бага. Гаралт: дараагийн үеүүдийн root cause баталгаа.

---

## Үе 1. #2 — Олон тоогоор хэвлэхэд эхний хэд зөв, дараа нь дизайн алдаатай

### Root cause (батлагдсан)
- Хэвлэх default = `WysiwygRaster`; шошго бүр том `^GFA` bitmap.
  `PrinterConfiguration.cs:36`, `LabelPreviewViewModel.cs:1951`
- Quantity>1 үед шошго бүрийг **тусдаа spooler job** болгон илгээдэг.
  `PrintService.cs:457-514`
- `WaitForJobCompletionAsync` нь RAW дамжуулалтад job алга болмогц (win32 1804)
  **шууд "Success"** буцаадаг — принтер физикээр хэвлэж дуусахаас өмнө.
  `RawPrinterHelper.cs:296-303, 316-323`
- Шошго хооронд ердөө 100ms/300ms хүлээдэг (1 растер шошго 1-3 сек).
  `LabelPreviewViewModel.cs:2149`
- → Job-ууд принтерийн буферт хэвлэхээс хурдан овоорч, буфер дүүрмэгц
  дараагийн ZPL тасарч гажиг шошго гарна.

### Засвар
**A (зөвлөмж) — Ижил шошгуудыг нэг job болгох:**
- RFID-гүй эсвэл шошго бүр ижил тохиолдолд: нэг `^GFA` зураг + `^PQ{n}`
  үүсгэж принтерээр N хувь хэвлүүлэх. Buffer race арилна.
- `GenerateZplInternal`-д WysiwygRaster горимд `^PQ{quantity}` аль хэдийн
  бичигддэг тул detailed-tracking замыг тойрч, нэг job илгээх.

**B — Шошго бүр өөр (sequential RFID) тохиолдолд:**
- Бүх `^XA…^XZ` блокийг **нэг spooler job-д** нэгтгэн илгээх (нэг WritePrinter),
  принтерийн дотоод дараалалд flow-control даатгах.
- ЭСВЭЛ жинхэнэ flow control: `~HQES` / `~HS` host-status query-ээр принтер
  бэлэн болохыг хүлээх (зүгээр spooler job алга болохыг биш).

**C — Хуурамч completion засах:**
- `WaitForJobCompletionAsync`-ийн 1804=Success логикийг (B) хэрэгжүүлбэл
  гол замаас хасах, эсвэл бодит хэвлэлт батлах хүртэл "soft success" болгох.

Файл: `PrintService.cs`, `ZplGeneratorService.cs`, `RawPrinterHelper.cs`,
`LabelPreviewViewModel.cs`
Тест: 1, 5, 20, 50 ширхэгээр хэвлэх; RFID-тэй ба RFID-гүй; CP30/ZT610.
Verify: бүх шошго ижил, гажиггүй гарч байгааг физикээр шалгах.

---

## Үе 2. #1 — Хэвлэсэн бараа "хэвлэгдээгүй" төлөвт үлдэх

### Root cause (батлагдсан)
1. **Silent success masking** — `IsSuccessfulUpdatePrintStatusResponse` нь
   хариуг танихгүй/хоосон үед ч `true` буцаадаг. Сервер татгалзсан ч апп
   "OK" гэж бодоод серверт "хэвлэгдээгүй" хэвээр.
   `ApiService.cs:614-647`
2. **Logging алга** — яагаад амжилтгүй болсныг харах боломжгүй (Үе 0 шийднэ).
3. **Олон ширхэгт зөвхөн эх RFID шинэчилнэ** — label 2..N нь sequential RFID-аар
   кодлогддог ч зөвхөн `SelectedProduct`-ийн эх RFID нэг л удаа шинэчлэгддэг.
   `PrintService.cs:466-475` vs `LabelPreviewViewModel.cs:2202`

### Засвар
- Үе 0-ийн логоор бодит хариуг хармагц зөв root cause-ийг батлах.
- `IsSuccessfulUpdatePrintStatusResponse`: "танихгүй бол true"-г болиулж,
  backend-ийн жинхэнэ амжилтын талбараар (ABP `result`/`success`) хатуу шалгах.
  Хоосон 200-г warning-той soft-pass болгох эсэхийг backend хариунаас шийдэх.
- Олон ширхэг хэвлэлтэд: бодитоор хэвлэгдсэн RFID бүрийг (`LabelResults`)
  тус тусд нь `UpdatePrintStatusAsync`-аар шинэчлэх. Sequential RFID-ийн
  логик backend-ийн жинхэнэ ProductRfid бичлэгтэй таарч буй эсэхийг шалгах
  (Үе 5/#4-тэй давхцаж магадгүй).
- `DateTime.Now` (local) сериалчлалыг backend timezone-той тулгаж шалгах.

Файл: `ApiService.cs`, `LabelPreviewViewModel.cs` (2183-2333, 2412-2450)
Тест: 1 ширхэг хэвлээд серверийн `isPrint` 2 болсныг batлах; олон ширхэгт
бүх RFID шинэчлэгдсэн эсэх; sync амжилтгүй үед UI үнэн анхааруулга өгөх.

---

## Үе 3. #3 — Баркод дизайнаасаа өөр; урт текст гарахгүй/шошгоноос хальж

### Root cause (батлагдсан)
1. **Preview ≠ Print renderer** — дэлгэц converter-уудаар, хэвлэлт
   `LabelRenderEngine`-ээр → хэмжээ зөрөх (drift).
2. **Багтаах логик алга** — растерт элемент бүр өөрийн хайрцагт хатуу clip
   болдог тул урт текст/баркод тасарч алга болно.
   `LabelRenderEngine.cs:93, 223-229`
3. **Legacy ZPL дээр хальдаг** — `^FB{width},{maxLines}` (10 мөр) болон `^BC`
   баркодны өргөн дата-ны уртаас хамаарч элемент/шошгоны хязгаараас хальна.
   `ZplGeneratorService.cs:251, 299`

### Засвар
- **Нэг renderer:** preview canvas-ийг ч `LabelRenderEngine`-ээр зуруулах,
  эсвэл converter болон engine-ийн баркод/текст метрикийг 1:1 нийцүүлэх.
- **Fit/auto-shrink:** баркод/текстийг хэмжиж элемент болон шошгоны хязгаарт
  багтаахаар module width / font size-ийг автоматаар багасгах.
- **Баркод өргөн хязгаарлах:** зурах өргөнийг `min(элемент өргөн, шошго − margin)`
  болгож, дата хэт урт бол module width-ийг багасгах эсвэл анхааруулах.
- **Pre-print validation:** элемент шошгоны хязгаараас халих гэж байвал
  хэвлэхээс өмнө хэрэглэгчид анхааруулга харуулах.
- Legacy ZPL замыг хадгалах бол `^FB` width-ийг шошгоны хэмжээгээр clamp хийх.

Файл: `LabelRenderEngine.cs`, `LabelSizeHelper.cs`, `ZplGeneratorService.cs`,
preview converter-ууд, `LabelPreviewView.xaml(.cs)`
Тест: богино/урт нэр, урт баркод дата, төрөл бүрийн шошгоны хэмжээ; preview
ба физик хэвлэлт ижил эсэх; баркод scanner-аар уншигдах эсэх.

---

## Үе 4. #5 — Server-side filter (ШИНЭ БОЛОМЖ)

### Шаардлага (client)
Fetch Data товч дархад дараах параметрээр **серверээс шүүж** татах:
- RFID статус
- Принтер статус (хэвлэгдсэн/үгүй)
- Үүсгэсэн огноо (мужаар)

### Одоогийн байдал
- `ResourceRequest`-д `Filter` талбар алга (`requireTotalCount/skip/take/key/
  joins/sort/searchOperation` л бий). `ResourceModels.cs:13-38`
- Одоо бүх барааг 1000-аар нь chunk-лан **бүхэлд нь** татаад client талд шүүдэг.
  `LabelPreviewViewModel.cs:1551-1585`

### Засвар
- `ResourceRequest`-д DevExtreme/ABP-style `filter` талбар нэмэх (жишээ:
  `[["product_rfid.is_print","=",1],"and",["...CreationTime",">=","2026-06-01"]]`).
  Backend-ийн filter format-ийг (Үе 0 trace + backend source-оор) батлах.
- `ApiService.GetResourcesAsync`-д filter параметр дамжуулдаг болгох.
- UI: Fetch Data дээр RFID/print status combobox + огнооны мужийн сонголт нэмэх.
- Server-side filter ажиллавал client-side бүрэн татах ачааллыг бууруулна
  (#5-ийн performance ч сайжирна).

Файл: `ResourceModels.cs`, `ApiService.cs`, `IApiService.cs`,
`LabelPreviewViewModel.cs`, холбогдох View.
Тест: статус/огноогоор шүүхэд серверийн хариу зөв ширхэгтэй ирэх; totalCount зөв.

---

## Үе 5. #4 — EPC шинэ бараа "printer program"-д шууд орж ирэхгүй

### Одоогийн дүгнэлт
- Client тал бус, **backend/SAP sync-тэй** холбоотой магадлал өндөр.
- Апп `EnqueueBranchSync`/`EnqueueEquipmentSync` гэсэн фон дараалал ашигладаг
  ("процесс эхэллээ, түр хүлээгээд дахин шалгана уу").
  `LabelPreviewViewModel.cs:1674-1709`
- `GetResourcesAsync` нь CreationTime desc эрэмбэлдэг тул шинэ бараа дээр гарах
  ёстой → асуудал нь серверийн жагсаалтад өгөгдөл тухайн үед ороогүйд байна.

### Хийх (backend лог хандалттай тул)
1. EPC бараа үүсгэсний дараа `api_response.txt` trace + серверийн лог шалгаж,
   шинэ бараа серверийн хариунд хэзээ ордогийг тогтоох.
2. Хэрэв шөнийн/товлосон job-аас шалтгаалбал: тухайн job-ийн хуваарь, эсвэл
   EPC үүсгэх үед шууд push хийдэг эсэхийг backend-аас тодруулах.
3. Боломжтой бол client-ээс "одоо синк хийх" дуудлага (Enqueue*Sync) ажиллуулаад
   үр дүнг ил гаргах; cache/replica lag эсэхийг шалгах.
4. Timezone filter (UTC vs +08) сервер талд байгаа эсэхийг шалгах.

Файл: ихэвчлэн backend; client талд trace/refresh сайжруулалт.
Гаралт: root cause backend талд бол тийш escalate, client засвар шаардлагатай
бол тусдаа task.

---

## Нийт эрсдэл ба нэмэлт тэмдэглэл
- Sequential RFID fabricated increment (`PrintService.cs:56-89`) нь backend-ийн
  жинхэнэ ProductRfid бичлэгтэй таарахгүй байж болзошгүй — #1, #4-тэй уялдуулж
  дахин үнэлэх.
- Бүх засварыг тусдаа commit-аар, нэг нэгээр нь verify хийж хийх.

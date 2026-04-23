# API SCP.StorageFSC

Документ описывает HTTP API сервиса файлового хранилища. Основан на `doc/openapi.txt` и коде контроллеров:

- `SCP.StorageFSC.Controllers.FileStorageController`
- `scp.filestorage.Controllers.MultipartController`
- `SCP.StorageFSC.Controllers.TenantAdminController`

Базовый URL из OpenAPI:

```text
http://localhost:5770/
```

## Авторизация

Все прикладные эндпоинты проходят через middleware API-token авторизации. Swagger/OpenAPI и health-эндпоинты пропускаются без проверки.

В каждом запросе нужно передавать API token. `X-Tenant-Id` используется как контекст tenant-а и зависит от типа токена:

| Заголовок | Описание |
| --- | --- |
| `X-Api-Token` | Plain-text API token. На сервере сравнивается SHA-256 хеш токена. |
| `X-Tenant-Id` | GUID tenant-а. Для обычного token-а обязателен и должен совпадать с tenant-ом токена. Для admin token-а необязателен; если передан, admin-запрос выполняется в контексте указанного tenant-а. |

Для обычного token-а tenant определяется по `token.TenantId`, но сервер дополнительно проверяет, что `X-Tenant-Id` передан и совпадает с `TenantGuid` найденного tenant-а. Для admin token-а `X-Tenant-Id` можно не передавать; в этом случае request context будет без конкретного tenant-а.

Типовые ошибки авторизации:

| Код | Когда возвращается |
| --- | --- |
| `401 Unauthorized` | Нет токена, token не найден, выключен, отозван, истек, не привязан к tenant-у, tenant не найден или неактивен, либо `X-Tenant-Id` отсутствует/не совпадает для обычного token-а. |
| `403 Forbidden` | Токен аутентифицирован, но ему не хватает прав или он обращается к чужому tenant-у. |

Права токена:

| Право | Назначение |
| --- | --- |
| `CanRead` | Чтение данных. |
| `CanWrite` | Создание/изменение данных. |
| `CanDelete` | Удаление данных. |
| `IsAdmin` | Административный доступ. |

## FileStorage API

Маршруты находятся под префиксом `/api/file`.

### Загрузить файл

```http
POST /api/file/upload
Content-Type: multipart/form-data
```

Загружает файл для текущего tenant-а.

Поля формы:

| Поле | Тип | Обязательное | Описание |
| --- | --- | --- | --- |
| `file` | file | Да | Файл для загрузки. Пустой файл отклоняется. |
| `category` | string | Нет | Категория файла. |
| `externalKey` | string | Нет | Внешний ключ для связи с бизнес-сущностью. |

Ответы:

| Код | Ответ |
| --- | --- |
| `200 OK` | `SaveFileResult` при успешной загрузке. |
| `400 Bad Request` | Ошибка валидации, например файл не передан. |
| `403 Forbidden` | Нет доступа. |
| `409 Conflict` | Дубликат файла. |
| `500 Internal Server Error` | Ошибка хранения или БД. |

Пример:

```bash
curl -X POST "http://localhost:5770/api/file/upload" \
  -H "X-Api-Token: <token>" \
  -H "X-Tenant-Id: <tenant-guid>" \
  -F "file=@report.pdf" \
  -F "category=docs" \
  -F "externalKey=order-123"
```

### Получить список файлов tenant-а

```http
GET /api/file
```

Возвращает список `StoredTenantFileDto` для текущего tenant-а.

Ответы:

| Код | Ответ |
| --- | --- |
| `200 OK` | Массив `StoredTenantFileDto`. |

### Получить информацию о файле

```http
GET /api/file/{fileGuid}
```

Параметры пути:

| Параметр | Тип | Описание |
| --- | --- | --- |
| `fileGuid` | uuid | GUID файла в tenant-хранилище. |

Ответы:

| Код | Ответ |
| --- | --- |
| `200 OK` | `StoredTenantFileDto`. |
| `404 Not Found` | Файл не найден. |

### Скачать файл

```http
GET /api/file/{fileGuid}/download
```

Возвращает поток файла с `Content-Type` из метаданных файла или `application/octet-stream`, если тип неизвестен.

Ответы:

| Код | Ответ |
| --- | --- |
| `200 OK` | Содержимое файла. |
| `404 Not Found` | Файл не найден. |

### Удалить файл

```http
DELETE /api/file/{fileGuid}
```

Удаляет файл tenant-а.

Ответы:

| Код | Ответ |
| --- | --- |
| `204 No Content` | Файл удален. |
| `404 Not Found` | Файл не найден. |

### Очистить orphan-файлы

```http
POST /api/file/cleanup-orphans
```

Удаляет физические файлы, которые не связаны с записями хранилища.

Ответы:

| Код | Ответ |
| --- | --- |
| `200 OK` | `{ "deletedCount": number }` |

## Multipart API

Маршруты находятся под префиксом `/api/multipart`.

Multipart-загрузка состоит из шагов:

1. Инициализировать сессию через `POST /api/multipart/init`.
2. Загрузить части через `PUT /api/multipart/{uploadId}/parts/{partNumber}` или `POST /api/multipart/part`.
3. Проверить статус через `GET /api/multipart/{uploadId}/status`.
4. Завершить сборку через `POST /api/multipart/complete`.
5. При необходимости отменить загрузку через `POST /api/multipart/{uploadId}/abort`.

Статусы multipart-сессии:

| Значение | Код | Описание |
| --- | --- | --- |
| `Created` | `0` | Сессия создана. |
| `Uploading` | `1` | Идет загрузка частей. |
| `Completing` | `2` | Выполняется сборка файла. |
| `Completed` | `3` | Файл собран. |
| `Aborted` | `4` | Загрузка отменена. |
| `Failed` | `5` | Ошибка загрузки или сборки. |
| `Expired` | `6` | Сессия истекла. |

Статусы части:

| Значение | Код |
| --- | --- |
| `Pending` | `0` |
| `Uploaded` | `1` |
| `Verified` | `2` |
| `Failed` | `3` |

### Инициализировать multipart-загрузку

```http
POST /api/multipart/init
Content-Type: application/json
```

Тело запроса: `InitMultipartUploadRequestDto`.

| Поле | Тип | Обязательное | Описание |
| --- | --- | --- | --- |
| `fileName` | string | Да | Имя исходного файла. |
| `fileSize` | int64 | Да | Полный размер файла в байтах. |
| `contentType` | string/null | Нет | MIME type файла. |
| `partSize` | int64 | Да | Размер одной части в байтах. |
| `expectedChecksumSha256` | string/null | Нет | Ожидаемый SHA-256 всего файла. |
| `tenantId` | uuid | Да | Tenant ID. |
| `metadata` | object/null | Нет | Дополнительные строковые метаданные. |
| `expiresAtUtc` | date-time/null | Нет | Время истечения сессии в UTC. |

Ответ `200 OK`: `InitMultipartUploadResultDto`.

| Поле | Тип |
| --- | --- |
| `uploadId` | uuid |
| `tenantId` | uuid |
| `fileName` | string |
| `fileSize` | int64 |
| `partSize` | int64 |
| `totalParts` | int32 |
| `status` | `MultipartUploadStatus` |
| `createdAtUtc` | date-time |
| `expiresAtUtc` | date-time/null |

Ошибки:

| Код | Когда возвращается |
| --- | --- |
| `400 Bad Request` | Ошибка валидации. |
| `500 Internal Server Error` | Ошибка инициализации. |

Пример:

```bash
curl -X POST "http://localhost:5770/api/multipart/init" \
  -H "X-Api-Token: <token>" \
  -H "X-Tenant-Id: <tenant-guid>" \
  -H "Content-Type: application/json" \
  -d '{
    "fileName": "video.mp4",
    "fileSize": 734003200,
    "contentType": "video/mp4",
    "partSize": 10485760,
    "tenantId": "<tenant-id>",
    "metadata": {
      "source": "mobile"
    }
  }'
```

### Загрузить часть по route-параметрам

```http
PUT /api/multipart/{uploadId}/parts/{partNumber}
Content-Type: multipart/form-data
```

Параметры пути:

| Параметр | Тип | Описание |
| --- | --- | --- |
| `uploadId` | uuid | ID multipart-сессии. |
| `partNumber` | int32 | Номер части. |

Поля формы:

| Поле | Тип | Обязательное | Описание |
| --- | --- | --- | --- |
| `file` | file | Да | Содержимое части. |
| `partChecksumSha256` | string | Нет | SHA-256 части. |

Ответ `200 OK`: `UploadMultipartPartResultDto`.

| Поле | Тип |
| --- | --- |
| `uploadId` | uuid |
| `partNumber` | int32 |
| `offsetBytes` | int64 |
| `sizeInBytes` | int64 |
| `storageKey` | string |
| `checksumSha256` | string/null |
| `status` | `MultipartUploadPartStatus` |
| `uploadedAtUtc` | date-time/null |

Ошибки:

| Код | Когда возвращается |
| --- | --- |
| `400 Bad Request` | Файл не передан, пустой файл или ошибка валидации. |
| `404 Not Found` | Multipart-сессия не найдена. |
| `500 Internal Server Error` | Ошибка загрузки части. |

Пример:

```bash
curl -X PUT "http://localhost:5770/api/multipart/<upload-id>/parts/1" \
  -H "X-Api-Token: <token>" \
  -H "X-Tenant-Id: <tenant-guid>" \
  -F "file=@part-001.bin" \
  -F "partChecksumSha256=<sha256>"
```

### Загрузить часть через form-поля

```http
POST /api/multipart/part
Content-Type: multipart/form-data
```

Поля формы:

| Поле | Тип | Обязательное | Описание |
| --- | --- | --- | --- |
| `uploadId` | uuid | Да | ID multipart-сессии. |
| `partNumber` | int32 | Да | Номер части. |
| `file` | file | Да | Содержимое части. |
| `partChecksumSha256` | string | Нет | SHA-256 части. |

Ответы такие же, как у `PUT /api/multipart/{uploadId}/parts/{partNumber}`.

### Получить статус multipart-загрузки

```http
GET /api/multipart/{uploadId}/status
```

Ответ `200 OK`: `MultipartUploadStatusDto`.

| Поле | Тип |
| --- | --- |
| `uploadId` | uuid |
| `tenantId` | uuid |
| `fileName` | string |
| `normalizedFileName` | string |
| `extension` | string |
| `contentType` | string/null |
| `totalFileSize` | int64 |
| `partSize` | int64 |
| `totalParts` | int32 |
| `uploadedPartCount` | int32 |
| `uploadedParts` | int32[] |
| `status` | `MultipartUploadStatus` |
| `errorCode` | string/null |
| `errorMessage` | string/null |
| `tempStoragePrefix` | string |
| `createdAtUtc` | date-time |
| `updatedAtUtc` | date-time/null |
| `completedAtUtc` | date-time/null |
| `expiresAtUtc` | date-time/null |
| `storedFileId` | uuid/null |

Ошибки:

| Код | Когда возвращается |
| --- | --- |
| `404 Not Found` | Multipart-сессия не найдена. |
| `500 Internal Server Error` | Ошибка получения статуса. |

### Завершить multipart-загрузку

```http
POST /api/multipart/complete
Content-Type: application/json
```

Тело запроса: `CompleteMultipartUploadRequestDto`.

| Поле | Тип | Обязательное | Описание |
| --- | --- | --- | --- |
| `uploadId` | uuid | Да | ID multipart-сессии. |

Ответ `200 OK`: `CompleteMultipartUploadResultDto`.

| Поле | Тип |
| --- | --- |
| `uploadId` | uuid |
| `tenantId` | uuid |
| `fileName` | string |
| `fileSize` | int64 |
| `contentType` | string/null |
| `finalChecksumSha256` | string/null |
| `physicalPath` | string |
| `relativePath` | string |
| `completedAtUtc` | date-time |
| `status` | `MultipartUploadStatus` |
| `storedFileId` | uuid/null |

Ошибки:

| Код | Когда возвращается |
| --- | --- |
| `400 Bad Request` | Сборка отклонена, например не все части загружены. |
| `404 Not Found` | Multipart-сессия не найдена. |
| `500 Internal Server Error` | Ошибка сборки. |

Пример:

```bash
curl -X POST "http://localhost:5770/api/multipart/complete" \
  -H "X-Api-Token: <token>" \
  -H "X-Tenant-Id: <tenant-guid>" \
  -H "Content-Type: application/json" \
  -d '{ "uploadId": "<upload-id>" }'
```

### Отменить multipart-загрузку

```http
POST /api/multipart/{uploadId}/abort
```

Ответ `200 OK`: `AbortMultipartUploadResultDto`.

| Поле | Тип |
| --- | --- |
| `uploadId` | uuid |
| `status` | `MultipartUploadStatus` |
| `updatedAtUtc` | date-time |

Ошибки:

| Код | Когда возвращается |
| --- | --- |
| `400 Bad Request` | Отмена отклонена из-за текущего состояния сессии. |
| `404 Not Found` | Multipart-сессия не найдена. |
| `500 Internal Server Error` | Ошибка отмены. |

## Tenant Admin API

Маршруты находятся под префиксом `/api/admin`.

### Получить список tenant-ов

```http
GET /api/admin/tenants
```

Доступ: только admin token с правом `Admin`.

Ответы:

| Код | Ответ |
| --- | --- |
| `200 OK` | Массив `TenantDto`. |
| `403 Forbidden` | Недостаточно прав. |

### Создать tenant

```http
POST /api/admin/tenants
Content-Type: application/json
```

Доступ: только admin token с правом `Admin`.

Тело запроса: `CreateTenantRequest`.

| Поле | Тип | Обязательное | Описание |
| --- | --- | --- | --- |
| `name` | string | Да | Название tenant-а. |

Ответы:

| Код | Ответ |
| --- | --- |
| `201 Created` | `TenantDto`. |
| `400 Bad Request` | Ошибка валидации. |
| `403 Forbidden` | Недостаточно прав. |
| `409 Conflict` | Tenant уже существует или конфликт бизнес-правила. |

Пример:

```bash
curl -X POST "http://localhost:5770/api/admin/tenants" \
  -H "X-Api-Token: <admin-token>" \
  -H "Content-Type: application/json" \
  -d '{ "name": "Acme" }'
```

### Получить текущий tenant

```http
GET /api/admin/tenant/me
```

Доступ: аутентифицированный token с правом `Read`.

Ответы:

| Код | Ответ |
| --- | --- |
| `200 OK` | `TenantDto`. |
| `403 Forbidden` | Недостаточно прав. |
| `404 Not Found` | Текущий tenant не найден. |

### Получить tenant по ID

```http
GET /api/admin/tenants/{tenantId}
```

Доступ: только admin token с правом `Admin`.

Параметры пути:

| Параметр | Тип |
| --- | --- |
| `tenantId` | uuid |

Ответы:

| Код | Ответ |
| --- | --- |
| `200 OK` | `TenantDto`. |
| `403 Forbidden` | Недостаточно прав. |
| `404 Not Found` | Tenant не найден. |

### Выключить tenant

```http
POST /api/admin/tenants/{tenantId}/disable
```

Доступ: только admin token с правом `Admin`.

Ответы:

| Код | Ответ |
| --- | --- |
| `204 No Content` | Tenant выключен. |
| `403 Forbidden` | Недостаточно прав. |
| `404 Not Found` | Tenant не найден. |

### Получить токены tenant-а

```http
GET /api/admin/tenants/{tenantId}/tokens
```

Доступ: аутентифицированный token с правом `Read`. Admin может смотреть токены любого tenant-а; обычный tenant может смотреть только свои.

Ответы:

| Код | Ответ |
| --- | --- |
| `200 OK` | Массив `ApiTokenDto`. |
| `403 Forbidden` | Недостаточно прав или обращение к чужому tenant-у. |

### Создать API token

```http
POST /api/admin/tokens
Content-Type: application/json
```

Доступ: аутентифицированный token с правом `Write`. Admin может создать токен для любого tenant-а; обычный tenant - только для себя.

Тело запроса: `CreateApiTokenRequest`.

| Поле | Тип | Обязательное | Описание |
| --- | --- | --- | --- |
| `tenantId` | uuid | Да | Tenant, для которого создается токен. |
| `name` | string | Да | Название токена. |
| `canRead` | boolean | Нет | Разрешение чтения. По умолчанию `true`. |
| `canWrite` | boolean | Нет | Разрешение записи. |
| `canDelete` | boolean | Нет | Разрешение удаления. |
| `isAdmin` | boolean | Нет | Административный токен. |
| `expiresUtc` | date-time/null | Нет | Время истечения токена в UTC. |

Ответ `201 Created`: `CreatedApiTokenResult`.

| Поле | Тип | Описание |
| --- | --- | --- |
| `token` | `ApiTokenDto` | Метаданные созданного токена. |
| `plainTextToken` | string | Единственный раз, когда сервер возвращает plain-text token. Его нужно сохранить на стороне клиента. |

Ошибки:

| Код | Когда возвращается |
| --- | --- |
| `400 Bad Request` | Ошибка валидации. |
| `403 Forbidden` | Недостаточно прав или попытка создать токен для чужого tenant-а. |
| `404 Not Found` | Tenant не найден. |

### Отозвать API token

```http
POST /api/admin/tokens/{tokenId}/revoke
```

Доступ: аутентифицированный token с правом `Write`. Admin может отзывать любой токен; обычный tenant - только свои токены.

Ответы:

| Код | Ответ |
| --- | --- |
| `204 No Content` | Токен отозван. |
| `403 Forbidden` | Недостаточно прав или попытка отозвать чужой токен. |
| `404 Not Found` | Токен не найден. |

## DTO

### `StoredTenantFileDto`

| Поле | Тип |
| --- | --- |
| `tenantFileId` | uuid |
| `fileGuid` | uuid |
| `tenantId` | uuid |
| `storedFileId` | uuid |
| `fileName` | string |
| `category` | string/null |
| `externalKey` | string/null |
| `contentType` | string/null |
| `fileSize` | int64 |
| `sha256` | string |
| `crc32` | string |
| `createdUtc` | date-time |

### `SaveFileResult`

| Поле | Тип |
| --- | --- |
| `success` | boolean |
| `status` | `SaveFileStatus` |
| `errorCode` | string/null |
| `errorMessage` | string/null |
| `file` | `StoredTenantFileDto`/null |
| `isDeduplicated` | boolean |
| `alreadyExistsForTenant` | boolean |

`SaveFileStatus`:

| Значение | Код |
| --- | --- |
| `Success` | `0` |
| `ValidationError` | `1` |
| `AccessDenied` | `2` |
| `StorageFailed` | `3` |
| `DatabaseFailed` | `4` |
| `DuplicateFile` | `5` |
| `AlreadyExists` | `6` |

### `TenantDto`

| Поле | Тип |
| --- | --- |
| `id` | uuid |
| `tenantGuid` | uuid |
| `name` | string |
| `isActive` | boolean |
| `createdUtc` | date-time |
| `updatedUtc` | date-time/null |

### `ApiTokenDto`

| Поле | Тип |
| --- | --- |
| `id` | uuid |
| `tenantId` | uuid |
| `name` | string |
| `tokenPrefix` | string |
| `isActive` | boolean |
| `canRead` | boolean |
| `canWrite` | boolean |
| `canDelete` | boolean |
| `isAdmin` | boolean |
| `createdUtc` | date-time |
| `lastUsedUtc` | date-time/null |
| `expiresUtc` | date-time/null |
| `revokedUtc` | date-time/null |

## Замечания по OpenAPI

- OpenAPI описывает загрузку частей `PUT /api/multipart/{uploadId}/parts/{partNumber}` и `POST /api/multipart/part` как `application/x-www-form-urlencoded`, но контроллеры принимают `IFormFile`, поэтому фактически нужен `multipart/form-data`.
- `POST /api/file/cleanup-orphans` помечен в XML-комментарии как admin-only, но в контроллере нет `TenantAccess(AdminOnly, Admin)`. Сейчас endpoint защищен общим API-token middleware, но не отдельной проверкой admin-прав.

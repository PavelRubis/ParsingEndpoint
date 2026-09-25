# ParsingEndpoint

.NET 10 REST API для обработки HTML из JSON-запроса.

## Запуск

Из корня проекта выполните:

```sh
docker compose up -d
```
Исходники API монтируются в контейнер; при каждом старте он восстанавливает зависимости, собирает приложение, запускает Yuniql и затем запускает API. Yuniql применяет SQL из `src/ParsingEndpoint/Database/Migrations` к существующей базе `postgres`. Данные PostgreSQL хранятся в именованном томе, смонтированном в `/var/lib/postgresql`.
Всего compose запускает три сервиса: API, PostgreSQL 18 и pgAdmin.

- API: <http://localhost:8090/api/parse>
- Swagger UI: <http://localhost:8090/api/swagger>
- pgAdmin: <http://localhost:8080>

pgAdmin работает в однопользовательском режиме и открывает заранее добавленный сервер без ввода пароля. PostgreSQL наружу не публикуется.

## Запрос и ответ

`POST /api/parse`, `Content-Type: application/json`. Поля запроса: `selector`, `attribute`, `url_b64`, `encrypted_text_bytes_b64`, `key_bytes_b64`, `page_b64`. Два готовых запроса — `json_payload_1.txt` и `json_payload_2.txt`. Ответы работающего API сохранены в `json_result_1.txt` и `json_result_2.txt`.

Успешный ответ содержит `is_error: 0`, пустые `error_code` и `error_message`, числа `elements_count` и `emails_count`, декодированный `url`, `decrypted_plain_text`, `elements_attr_list` и `emails_list`. Для выбранного элемента без указанного атрибута в списке и БД сохраняется пустая строка. Повторный успешный запрос добавляет новые строки в `elements`.

Ошибки входных данных возвращают HTTP 400 и тот же набор полей с `is_error: 1`; внутренние ошибки возвращают HTTP 500. Некорректный JSON обрабатывается при привязке тела запроса к модели, остальные значения, включая UTF-8 внутри `url_b64` и `page_b64`, проверяются FluentValidation в сервисе. При нескольких ошибках код ответа — `MULTIPLE_VALIDATION_ERRORS`, а сообщения перечислены через перевод строки.

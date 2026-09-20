# Измерение реальных View в Unity

## Где начать

1. **Unity Editor на macOS, обычный WebView**: быстрый ручной проход по проекту, ошибки скриптов, legacy owner и корректность View. В текущем RenderViewsManager `UseEmbeddedWebView = false`; не включать рендер WebView в текстуру для этого сравнения.
2. **Тот же проект на iOS и Android**: замеры, по которым принимаем решения о скорости. Editor не воспроизводит мобильный запуск WebView, работу декодеров и память GPU.
3. Unity WebGL/TypeScript — отдельный последующий прогон. Его скорость не заменяет мобильные измерения.

Для старта достаточно собрать **C++ для Unity**, с уже исправленной загрузкой серверного bundle. На Mac обновить `Plugins/macOS/libBalancyCore.dylib`, на устройствах — соответствующий core plugin (iOS `.a`, Android `.so`). После замены загруженного нативного плагина перезапустить Editor. Также нужен текущий SDK целиком: C#, WebView plugin и сгенерированные bridge/WebGL ресурсы. Одно обновление C++ не добавляет эту диагностику.

В текущем локальном Unity-проекте ресурсы уже синхронизированы в StreamingAssets. Для другого проекта обновить SDK и пересобрать player. Исходники bridge находятся в TypeScript-репозитории, собирать TypeScript core вручную для обычного Unity native прогона не требуется.

## Как включить

В своём обработчике готовности SDK, до первого Prepare/Open:

```csharp
Balancy.RenderViewsManager.SetPerformanceLogging(true);
Balancy.RenderViewsManager.PrepareWebView(
    () => UnityEngine.Debug.Log("[BalancyPerf] scenario=prepared"),
    error => UnityEngine.Debug.LogError("[BalancyPerf] prepare failed: " + error)
);
```

Дальше открывать/закрывать View обычными существующими методами. Дублировать Prepare при каждом открытии не нужно. Выключение:

```csharp
Balancy.RenderViewsManager.SetPerformanceLogging(false);
```

Флаг выключен по умолчанию. Диагностика не передаёт owner, содержимое HTML, тексты локализаций и тела запросов. В лог попадают технический viewId, имя HTML-файла, длительности, размеры и счётчики. По одному JSON-summary на ready/ошибку/dispose вместо лога на каждую картинку. Host пишет этапы чтения, подготовки и отправки. В samples могут быть адреса ресурсов и параметры CSS, максимум 12 записей на View.

## Последовательность ручного прогона

| Сценарий | Действия | Что сравниваем |
|---|---|---|
| Обычный режим | Новая Play/player-сессия, включить логи, **не вызывать Prepare**, открыть A | Начальная навигация и classic bridge stages |
| Первый persistent | Новая сессия, включить логи, вызвать Prepare, дождаться callback, открыть A | `shellReady` отдельно от подготовки первого A |
| Тёплый A | Закрыть A, открыть A 10–20 раз | Медиана/p95 `openLocalToReady`, cache hits/misses |
| Разные окна | A → close → B → close → A; пройти весь набор View | Повторное использование ресурсов, соответствие owner, правильные тексты/картинки |
| Смена данных | Сменить язык/CMS штатным способом, закрыть и повторно открыть | Новые данные вместо старого кэша |
| Быстрое закрытие | Закрыть тяжёлый View во время подготовки, затем открыть другой | Нет позднего показа старого View, работает новое окно |
| Ошибки | Недоступный ресурс/отсутствующий prefab в тестовой конфигурации | Понятная ошибка, следующее открытие работает |
| Мобильный цикл | Свернуть/вернуть приложение, повторить A/B | Восстановление и отсутствие зависшего shell |

Перед каждой серией можно написать `Debug.Log("[BalancyPerf] scenario=warm-A")`. Вручную проверять кнопки/покупки в тестовом окружении, закрытие, анимации, звуки, вложенные prefab, динамические списки и ownerless/legacy View. Логи готовности сами по себе не доказывают визуальную корректность.

«Холодный WebView» не означает пустой дисковый кэш SDK. Отмечать отдельно: свежий процесс, прогретый shell, загруженные файлы/контент. Не очищать пользовательские данные ради каждого измерения.

## Как читать логи

Фильтр Unity Console: **`[BalancyPerf]`**. Сохранить целый Editor.log/player log для анализа; stack traces обычных Debug.Log лучше отключить на время серии. Не включать подробный native debug logging с содержимым сообщений — он добавляет лишнюю нагрузку. Сравнивать одинаковые сборки/устройства с одинаковыми настройками.

Host строки:

- `readScriptsBundle`: native getter + преобразование в C# строку; название API `CompileAllScripts` осталось, но при новом core возвращается серверный bundle.
- `readViewHtml`: чтение локального HTML. `htmlChars`/`scriptChars` — символы, не UTF-8 байты.
- `openLocalAccepted`: локальная подготовка закончена, строка связывает basename файла с viewId.
- `buildLoadViewPayload`: base64 и сериализация сообщения.
- `sendPersistentDispatch`: возврат native send, **не** завершение выполнения JS.
- `shellNavigationComplete`: callback загрузки shell до инъекции.
- `injectBridgeDispatch`: стоимость отправки кода на выполнение, **не** его исполнения.
- `shellReady`: полный host-интервал Prepare → ACK bridge, включая создание, навигацию и инициализацию.
- Замер `coreRequest` удалён: временная C#-лямбда небезопасна для асинхронного native callback. Для запросов используйте bridge-счётчик `requestTotalMs` (сумма задержек, не wall-clock) и длительности стадий ресурсов.
- `viewReadyReceived`: от принятия ShowView до ACK, включая ожидание shell/предыдущего clear и dispatch через Unity Tick.
- **`openLocalToReady`**: от входа OpenLocalView до готовности — основной показатель для сравнения persistent View; включает RefreshScripts и чтение HTML.
- `showCommand`: команда native show с `configuredDelayMs` и `configuredFadeMs`.
- `viewClearedReceived`: CloseView → ACK очистки DOM; shell при этом остаётся жив.
- `classicNavigationComplete`: обычное открытие → callback навигации; соответствующий bridge-summary имеет event `classicReady`.

JSON-summary bridge:

- `parseHtml`, `installScripts` / `scriptsReused`, `stylesheets`, `pageScripts`;
- `mandatoryLocalization`, `prefabExpansion`, `localization`, `fonts`, `imagesIncludingDecode`, `audio`, `lottie`, `dynamicText`;
- `componentDependencies`, `initializeComponents`, `rootInit`, `spawnedPreparations`, `prepareUI`, `dispose`;
- counters: `batches`, `requests`, `requestBytes` (UTF-8 исходящих batch), `requestErrors`, `cacheHits`, `cacheMisses`, `cacheJoined`, `imagesPrepared`;
- `imageUrlWaitTotalMs` — сумма ожидания URL в стадии применения, уже прогретая prefetch может дать почти ноль;
- `imageDecodeTotalMs` — сумма ожидания загрузки/decode картинок; это **не** измерение GPU upload/создания Unity Texture;
- `requestTotalMs` — сумма длительностей JS pending requests после регистрации; параллельные запросы перекрываются, поэтому сумма может превышать время открытия.

Повторные spans одного имени агрегируются: `ms` — сумма, `count` — число вызовов, `maxMs` — максимальный. Вложенные стадии включают время дочерних: **нельзя складывать все stages в общее время**. Есть ограничение 48 имён стадий; пропуски отражаются в `omittedSpans`. Момент завершения виден в event (`shellReady`, `classicReady`, `viewReady`, `viewDisposed`, `viewLoadError`).

JS и C# используют разные монотонные часы. Сравнивать локальные интервалы, связывать их по viewId; не вычитать timestamps разных сред. `elapsedMs` на dispose включает время, пока окно было открыто; стоимость очистки — span `dispose`.

## Границы измерений

`viewReady` означает окончание подготовки UI, **не** первый видимый кадр и не конец fade. По умолчанию задержка и fade равны нулю. Их значения логируются, фактический конец native-анимации в этой версии не измеряется. Fonts stage получает URL/декларацию, но не обещает завершения `document.fonts.ready`. Classic WebView и Unity WebGL имеют меньше host spans, чем native persistent.

Логирование само добавляет нагрузку. После поиска узкого места повторить сравнительный прогон с логированием выключенным. Мобильную память проверять отдельно Instruments/Android Profiler: CPU/JS heap, bitmap/GPU memory и процесс WebView; повторные ready/clear сами по себе не подтверждают отсутствие утечки.

## Что сохранить для совместного анализа

Лог одной сессии, модель устройства/ОС, режим (classic/persistent), список открытых View и сценариев, сборку SDK/core и замеченные визуальные проблемы. Начать с тяжёлого A и последовательности A → A → B → A. Для измерения загрузки bundle с сервера нужен отдельный интервал SDK preload; текущий `readScriptsBundle` начинается уже при обращении к локальному core getter.

Пауза до входа в OpenLocalView (например, preload файлов из C++ после клика) не входит в `openLocalToReady`. Если визуально есть задержка до первой host-строки, следующий шаг — добавить метки вокруг пользовательского OpenView и соответствующего C++ preload.


## Настройка появления — публичный API

После готовности SDK, до открытия или во время работы подготовленного shell:

```csharp
// Значения по умолчанию: 30 мс до показа и fade длительностью 80 мс.
Balancy.RenderViewsManager.SetViewDelays(0.03f, 0.08f);
// При необходимости можно вернуть полностью мгновенный показ.
Balancy.RenderViewsManager.SetViewDelays(0f, 0f);
```

Первый аргумент — задержка до начала показа, второй — длительность fade, оба в секундах. Отрицательные значения ограничиваются нулём. Вызов до инициализации SDK не сохраняет настройки. Изменение применимо и к скрытому persistent WebView.

После оптимизации bundle при повторном открытии `scriptsBase64Chars` должен отсутствовать/быть нулём, `scriptsReused` — 1. После изменения bundle или создания нового shell полная передача разрешена. Unity WebGL может передать bundle при первом открытии после prepare, затем переиспользует подтверждённую версию.

`requestCancelled` — штатная отмена при закрытии; `requestErrors` — остальные ошибки, с ограниченными samples. Этапы `localizationWait`, `localizationSanitize`, `localizationApply` уточняют затраты локализации. `prepareUI` включает ожидание параллельных prerequisites; не суммировать вложенные этапы. Native bundle пока читается перед открытием для корректности при обновлениях и legacy-загрузке.

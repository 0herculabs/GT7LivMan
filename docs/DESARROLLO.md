# GT7LivMan — notas de desarrollo

Detalles internos del proyecto: cómo está construido, por qué, y cómo ampliarlo. Para usar la app, ver el [README](../README.md).

## Compilar y ejecutar desde el código

Requiere el [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet build
dotnet run --project src\GT7LivMan.App
```

`dotnet build` copia `templates/*.json`, `fonts/` y `fonts-bundled/` junto al ejecutable automáticamente.

## Generar la versión distribuible

```powershell
dotnet publish src\GT7LivMan.App -c Release -o publish\GT7LivMan
Compress-Archive publish\GT7LivMan publish\GT7LivMan.zip -Force
```

El resultado es un único `GT7LivMan.exe` *self-contained* (win-x64, con el runtime de .NET dentro) más las carpetas `templates/`, `fonts/` y `fonts-bundled/` sueltas a su lado, porque la app las carga desde `AppContext.BaseDirectory`. Esos ficheros llevan `ExcludeFromSingleFile` en el `.csproj`: sin él, el empaquetado *single-file* metía los `.ttf` dentro del exe y `FontResolver` no los encontraba.

## Por qué el SVG tiene la forma que tiene

GT7 impone reglas estrictas sobre el SVG que acepta, y todo el pipeline de exportación está diseñado alrededor de ellas:

| Regla de GT7 | Cómo la cumplimos |
|---|---|
| ≤ 15 KB | Coordenadas redondeadas, sin metadatos/comentarios/IDs, agrupación por color de relleno |
| SVG 1.0/1.1 | `SvgWriter` declara `version="1.1"` explícitamente |
| Sin `<text>` | El texto de la matrícula se convierte a contornos (`<path>`) vía `GlyphTypeface`, nunca se emite `<text>` |
| Sin bitmaps embebidos | El modelo de datos no tiene ningún concepto de imagen rasterizada |
| Sin blend modes / gradientes | Solo rellenos planos — es un axioma del modelo (`RgbColor` no admite alpha ni blend) |
| Fondo transparente | Nunca se pinta un rectángulo de fondo que cubra todo el lienzo |

Como `SvgWriter` solo emite rellenos planos, el importador reconstruye los trazos del SVG original:

- Un `<rect>` con `fill` **y** `stroke` (el marco de la matrícula) → dos rectángulos redondeados concéntricos rellenos.
- Un `<rect>` con solo `stroke` (la línea decorativa interior de Portugal) → un anillo real: contorno exterior e interior con sentidos de giro opuestos, que bajo la regla *NonZero* dejan hueco el centro, igual que el agujero de una "O".

Así no dependemos ni de `stroke` ni de `rx` en `<rect>`, cuyo soporte en el parser de GT7 no está documentado.

## Presupuesto de bytes cuando la fuente no colabora

No todas las fuentes de matrícula tienen glifos igual de simples: en `LICENSE PLATE USA` (California), un "8" tiene 3 subtrazados y unos 84 segmentos (el contorno exterior más los dos contadores), muchísimo más detalle que un "7". Una matrícula del peor caso posible (repetir el carácter más complejo) puede llegar a duplicar el límite de 15 KB solo por el nivel de detalle de la fuente — no es un fallo del código, es una diferencia real de complejidad entre fuentes.

`ExportEscalation` es la red de seguridad, y se usa siempre (tanto en la vista previa como al exportar), para cualquier plantilla o fuente:

1. Exporta normal (1 decimal de precisión).
2. Si no entra, reintenta con 0 decimales.
3. Si sigue sin entrar, aplica una simplificación de Douglas-Peucker a las curvas (aplanadas primero a polilíneas) con tolerancias crecientes, hasta encontrar la más suave que quepa.

Cada nivel apunta a un objetivo interno de 14 KB, no al límite duro de 15 KB de GT7 — así "cabe por los pelos" nunca es el resultado final; siempre queda margen real.

## Tipografía: la rejilla de celdas

Las matrículas reales colocan cada carácter en una **celda de ancho fijo** — es lo que les da su aspecto regular, y de lo que dependen elementos del dibujo como los puntos separadores de Portugal. El pipeline reproduce eso:

- El avance del lápiz es el paso fijo de la plantilla (`Tracking`), no el ancho natural del glifo. Un separador literal de la máscara (el espacio de España, el guion de Países Bajos) usa su propio paso, más estrecho (`SpaceTracking`); un separador visible se dibuja centrado en esa celda.
- Cada glifo se **centra por su tinta** dentro de su celda. Sin esto, un carácter estrecho (un "1" mide la mitad que un "0") queda pegado al borde izquierdo y la fila entera parece desplazada, de forma visible con unas matrículas y no con otras.
- Si un glifo **no cabe** en su celda, se condensa horizontalmente. Una fuente real de matrícula (DIN 1451, MESPREG) está diseñada para que todo el alfabeto quepa, así que esto nunca se activa; con una fuente de respaldo proporcional, una "W" o una "M" desbordaría y descuadraría la fila.
- El bloque completo se ancla por su centro (`TextAnchor.Center`), así que no se mueve según qué caracteres se escriban.

## Arquitectura

```
GT7LivMan.sln
  src/GT7LivMan.Core             Sin dependencia de WPF: modelo geométrico, parser/writer SVG,
                                  modelo de plantilla (PlateDocument), validación
  src/GT7LivMan.Typography.Wpf   Contornos de glifos reales vía GlyphTypeface, resolución de fuentes
  src/GT7LivMan.Vectorization    Vectorización de raster: ImageTracer.NET vendorizado + wrapper fino
  src/GT7LivMan.App              Shell WPF: las dos pestañas, cada una con su propio ViewModel
  tests/GT7LivMan.Core.Tests             Tests puros (sin WPF)
  tests/GT7LivMan.Typography.Wpf.Tests   Tests de extremo a extremo con fuentes y plantillas reales
  tests/GT7LivMan.Vectorization.Tests    Tests de extremo a extremo raster → SVG exportado
  templates/*.json               Plantillas (generadas desde assets/base/*.svg por los tests)
  assets/base/                   SVG(s) de partida, uno por país/variante
  fonts/                         Fuentes de matrícula (no se versionan ni se publican — ver más abajo)
  fonts-bundled/                 Fuentes de respaldo libres (SIL OFL) que sí se distribuyen
```

Una plantilla se compone de arte estático (importado del SVG) más uno o varios **campos** editables. Un campo puede pintarse repartido en varios sitios: la fecha ITV portuguesa es un único campo de 4 dígitos que se reparte en dos líneas apiladas (`CharStart`/`CharCount`), mes sobre año. Un campo puede además declarar `DropdownOptions` (p. ej. los meses de California): la interfaz muestra un desplegable editable en vez de una caja de texto simple, pero sigue aceptando cualquier valor que cumpla la máscara, no solo los de la lista — salvo que además declare `RequireDropdownSelection` (el mes de California), en cuyo caso es una elección cerrada, porque una máscara "AAA" no puede por sí sola distinguir "un mes real" de "tres letras al azar".

Un campo puede ofrecer varios formatos con `AlternativeMasks`: Países Bajos usa `XX-XX-XX` (p. ej. `NL-01-AB`, el formato por defecto) y `XX-XXX-X` (`29-KTV-7`). Como los dos tienen seis caracteres, lo tecleado no dice cuál se quiere, así que la interfaz muestra un selector de formato encima de la caja de texto. El formato elegido pasa a ser la única máscara del campo (`FieldDef.WithMask`): los guiones se colocan solos para ese formato, la validación y "Matrícula aleatoria" lo respetan, y al cambiar de formato lo ya escrito se recoloca. `RandomizeAsAlternatingBlocks` hace que los grupos aleatorios alternen letras y dígitos, como en las matrículas holandesas reales.

`GT7LivMan.Core` no depende de WPF a propósito: el modelo de geometría, el parser/writer de SVG y la validación son 100% testeables sin necesidad de Windows/WPF, y dejan la puerta abierta a cambiar el motor de contornos de texto en el futuro si hiciera falta.

Un único modelo (`Scene`) alimenta tanto la vista previa en pantalla (`WpfSceneRenderer`) como la exportación (`SvgWriter`) — así un glifo mal formado o una transformación incorrecta se ve al instante en la vista previa, no solo al exportar.

**SVG Generator reutiliza el mismo pipeline sin cambiar nada en él**: un decal es solo un `PlateDocument` con `Fields: []` (ya previsto desde el diseño original — "decals libres: el mismo `PlateDocument` con `Fields` vacío"), así que `SceneCompiler`, `SvgWriter`, `ExportEscalation` y `WpfSceneRenderer` no se enteran de si el arte viene de una plantilla de país o de una imagen subida. Lo único nuevo son dos piezas que producen ese `Elements`:

- **`GenericSvgImporter`** (`GT7LivMan.Core/Svg/`): a diferencia de `SvgReader` (estricto a propósito, para el arte curado de las plantillas — lanza excepción ante cualquier cosa inesperada), este importador es de mejor esfuerzo: nunca lanza, y lo que no puede representar lo aproxima o lo omite, devolviendo una lista de avisos en español además de las formas. Entiende `rgb(...)` y nombres de color CSS además de hex, `style="fill:...` además de atributos de presentación, degradados aplanados a su color medio, y omite (avisando) texto/imágenes/`<use>`/trazos sin relleno.
- **`GT7LivMan.Vectorization`** (proyecto nuevo, `RasterVectorizer.cs`): envuelve [ImageTracer.NET](https://github.com/MiYanni/ImageTracer.NET) —código de terceros vendorizado bajo `ImageTracer/`, con su licencia Unlicense/dominio público incluida— y expone solo dos parámetros (número de colores, detalle). Es la primera dependencia NuGet del proyecto (`System.Drawing.Common`, necesaria para decodificar el JPG/PNG). Su salida se pasa por el mismo `GenericSvgImporter` que un SVG subido directamente — un único camino de importación para ambos orígenes.

Ese wrapper también corrige un bug real de la librería vendorizada: ImageTracer.NET formatea las coordenadas del SVG con la cultura del hilo actual, no invariante, así que en una máquina configurada en `es-ES` "90.5" salía como "90,5" y corrompía el trazado en silencio — `RasterVectorizer.Trace` fuerza cultura invariante solo durante la llamada.

### Por qué vectorizar una imagen real tarda milisegundos y no minutos

Los primeros JPG/PNG reales que se probaron tardaban minutos y disparaban la RAM a cientos de MB. La causa raíz está en el código vendorizado, no en el propio, y son **tres** problemas encadenados:

1. **`Options.ColorQuantization.NumberOfColors` es configuración muerta**: no se lee en ningún punto del camino de trazado real (`ImageTracer.ImageToSvg`). El control de "Colores" de la UI no hacía nada por sí solo.
2. **La separación en capas asigna una matriz del tamaño completo de la imagen por cada color de la paleta, no por cada color presente** — y la paleta que de verdad se usa es una fija de 256 colores (`BitmapPalettes.Halftone256`), siempre. Para una imagen de trabajo de 800×800 son 256 matrices de ~640.000 celdas cada una: de ahí el tiempo y la RAM, sin relación con la complejidad real de la imagen.
3. **El alfa cuenta como parte del color.** Un logo PNG descargado de internet tiene los bordes suavizados, así que contiene los 256 niveles de alfa posibles; como `ImageTracer.NET` identifica cada color por la tupla ARGB completa, cuantizar solo el RGB deja igualmente 256 "colores" distintos y el problema (2) intacto. El caso más extremo medido: un logo cuyo RGB era **un único color**, con toda la forma codificada en el canal alfa.

La solución, casi toda en `GT7LivMan.Vectorization`:

- **`RasterVectorizer`** reduce la imagen a un máximo de 800 px de lado antes de trazar (un decal plano no necesita más resolución).
- **`ColorQuantizer`** (nuevo, cuantización *median-cut* propia) reduce la imagen a los `colorCount` colores pedidos **antes** de pasarla al trazador, y **binariza el alfa**: cada píxel entra o no entra en el decal (GT7 no admite alfa en un relleno de todos modos). Así el número real de colores distintos queda acotado a `colorCount` + 1, pase lo que pase con los bordes del original.
- **`SmallRegionCleaner`** elimina antes del trazado las regiones conectadas de menos de 24 píxeles de área. Medir área permite quitar motas y franjas finas del suavizado sin perder detalles compactos: el filtro anterior medía longitud de contorno y borraba, por ejemplo, los puntos pequeños de la cola del logo de GitHub. `PathOmit` conserva ahora el valor base 8 de ImageTracer y sigue escalando con el control de "Simplificación".
- El único cambio dentro del código vendorizado: en vez de la paleta fija de 256 colores, `ImageTracer.cs` usa los colores que realmente aparecen en la imagen (ya reducidos por `ColorQuantizer`).

Además, `GenericSvgImporter` ya no dibuja opacas las formas totalmente transparentes: el fondo transparente de un logo trazado llega justo así, y pintarlo habría tapado el dibujo con un bloque sólido.

### Bordes limpios al cuantizar

El suavizado de una imagen puede introducir tonos casi idénticos en un mismo borde. Si se trazan por separado, forman franjas diminutas; al descartar algunas por su tamaño aparecen dientes y huecos incluso en líneas rectas. `ColorQuantizer` evita repartir un mismo valor de canal entre dos particiones y fusiona colores de paleta cuya distancia RGB es de hasta 24 antes de asignar los píxeles. El número de colores solicitado es un máximo: tonos muy próximos pueden quedar unidos. Después, `SmallRegionCleaner` elimina motas por área sin borrar detalles compactos. Hay pruebas de regresión con colores planos de áreas desiguales, un triángulo con pequeñas variaciones de verde y detalles desconectados similares a los puntos del logo de GitHub.

### Los huecos de las letras (la "e", la "o", la "a")

Un trazador de regiones no tiene el concepto de agujero: dibuja el contorno de cada zona de color plano como una forma cerrada independiente. El hueco de una "e" vuelve, por tanto, como una región **aparte** apilada encima del borrón macizo en que se convirtió la letra, y el dibujo solo se ve bien porque esa región se pinta después, tapándola.

Eso funciona mientras el hueco tenga un color que pintar. En un logo con fondo transparente el hueco *es* transparente: no hay nada que pintar, y la letra queda rellena. Por eso logos como el de Carrefour salían con las letras macizas.

`HoleCutter` lo resuelve añadiendo cada región transparente como un subtrazado más de la forma que la contiene, bajo la regla de relleno **EvenOdd**, que la recorta con independencia del sentido de giro de cada contorno — que es además como un logo vectorial habría descrito esa misma letra desde el principio. Dos detalles que importan:

- Un hueco tiene que **caber dentro** de la forma que perfora. Del fondo solo se traza su contorno exterior, y ese rectángulo, tomado por sí solo, encierra el dibujo entero: sin esa comprobación el fondo se recortaba *dentro* de la obra y los colores salían invertidos.
- Entre varias formas que contienen el hueco gana la **última pintada**, que es la que realmente se ve en ese punto.

Solo se aplica a imágenes rasterizadas. En un SVG hecho a mano una forma transparente simplemente no se dibuja, y perforar lo que hay debajo sería incorrecto.

Resultado medido sobre logos reales descargados de internet (Sparco 3840×2400, Red Bull, Monster, El Corte Inglés, Gran Turismo): **1–2 segundos y 1,6–13,5 KB cada uno**, todos dentro del presupuesto de GT7 y perfectamente reconocibles.

## Resolución de fuentes

`FontResolver` prueba primero la fuente preferida de la plantilla y luego sus alternativas; para cada nombre busca en este orden:

1. Carpeta `fonts/` junto al ejecutable (por nombre de archivo o por nombre interno de familia).
2. Fuentes instaladas en el sistema.
3. `fonts-bundled/`: alternativas libres con licencia SIL OFL que sí se distribuyen (Roboto Condensed y Overpass, con sus licencias incluidas).

`fonts/` es para fuentes de terceros con licencia de uso personal: está en el `.gitignore` (salvo `PUT_FONTS_HERE.txt`) y el `.csproj` las copia al compilar en local, pero nunca al publicar (`CopyToPublishDirectory=Never`), así que la release sale sin ellas.

Las alternativas empaquetadas son **condensadas a propósito**: los anchos de celda de las plantillas están calibrados para la proporción estrecha de una fuente de matrícula, y una fuente ancha obligaría a condensar casi todos los caracteres.

Para Portugal se probó una DIN 1451 "Breitschrift" (ancha, de señalización) y se descartó: da un resultado visiblemente distinto a una matrícula real. Hace falta la variante "Mittelschrift": la plantilla pide la familia exacta "Alte DIN 1451 Mittelschrift" (`din1451alt.ttf`), porque la descarga trae también `din1451alt G.ttf` (*gepraegt*, en relieve), que va antes por orden alfabético y la búsqueda por nombre aproximado la cogería primero.

En Carolina del Norte, la máscara `********` usa `*`: cualquier carácter (letra, dígito, símbolo o espacio), y los `*` del final pueden quedar vacíos, así que admite de 1 a 8 caracteres.

## Tests

```powershell
dotnet test
```

Incluye, entre otros:

- Conformidad con las reglas de GT7 (sin `<text>`/`<image>`/gradientes/blend/`stroke`, versión 1.1, fondo transparente).
- Invariancia de cultura: el exportador nunca debe usar coma decimal, ni siquiera en un sistema en `es-ES`.
- Ida y vuelta del parser de paths SVG.
- Importación de los SVG reales, incluyendo composición de transforms (`translate`+`rotate` de las 12 estrellas españolas) verificada con un punto calculado a mano.
- Que el paso de celda de Portugal deje los puntos separadores del dibujo exactamente centrados en los huecos de la máscara (`2·Tracking + SpaceTracking = 257`) — si alguien retoca esos números, el test explica por qué fallan.
- Que una "W" se condense para caber en su celda y quede centrada en ella.
- Que `ExportEscalation` recupere dentro del presupuesto casos que el escritor SVG normal no puede (curvas sintéticas con miles de detalles, y el peor glifo real de California), y que nunca lance excepción con trazados de regla `evenodd`.
- Que `PathDataSimplifier` conserve extremos y cierre de los subtrazados, y que la polilínea resultante no se aleje de la curva original más que la tolerancia pedida.
- Extremo a extremo real: plantilla + fuente reales → SVG exportado, validado contra las reglas de GT7 y el presupuesto de tamaño. Deja muestras en `out/` para abrirlas en el navegador o subirlas al Decal Uploader.
- `GenericSvgImporter`: que nunca lanza excepción pase lo que pase en el SVG de entrada (incluido XML corrupto, que sí da un `FormatException` limpio), que aplana degradados/ignora opacidad/omite texto-imagen-`<use>` avisando de cada uno, y que calcula el tamaño del lienzo desde `viewBox` con el offset correcto cuando no empieza en (0,0).
- `HoleCutter`: que una región transparente dentro de una forma se convierte en un hueco EvenOdd real, que el fondo transparente que rodea al dibujo **no** se recorta dentro de él (el fallo que invertía los colores), y que entre formas anidadas gana la última pintada. Más un test de extremo a extremo con un anillo sobre fondo transparente — el caso del hueco de una "e" — que comprueba que el agujero sobrevive hasta el SVG exportado.
- `RasterVectorizer`: que el resultado no cambia bajo `es-ES` frente a cultura invariante (el bug de formateo descrito arriba), extremo a extremo real (PNG sintético → `RasterVectorizer` → `GenericSvgImporter` → `ExportEscalation`) dentro de presupuesto, que un degradado se cuantiza de verdad al número de colores pedido, y que una imagen de 2000×2000 traza en segundos, no minutos (guarda de regresión del bug de rendimiento descrito arriba).

Los tests también **regeneran `templates/*.json`** desde los SVG de `assets/base/`, así que las plantillas nunca se desincronizan del arte de partida.

## Añadir un país

1. Deja su SVG en `assets/base/`.
2. Copia uno de los generadores de `tests/GT7LivMan.Core.Tests/Model/` (`SpainTemplateGeneratorTests`, `PortugalTemplateGeneratorTests`/`PortugalV1TemplateGeneratorTests`, `UkTemplateGeneratorTests`, `NetherlandsTemplateGeneratorTests` si la matrícula tiene varios formatos a elegir, `JapanTemplateGeneratorTests` si tiene varios campos en dos líneas y listas cerradas, o `CaliforniaTemplateGeneratorTests` si necesitas varios campos con parches de color como el mes/año) y ajusta máscara, campos y posiciones.
3. `dotnet test` regenera la plantilla; el desplegable de la app la recoge sola, sin tocar la interfaz.

Si el SVG trae cosas que `SvgReader` rechaza — texto sin convertir a contornos, `clip-path`, `opacity` — no hace falta retocarlo a mano: `DesignSvgFlattener` (en `tests/GT7LivMan.Typography.Wpf.Tests`, porque necesita WPF) contornea el texto con su fuente, recorta con el clip y mezcla la opacidad con el color de debajo. Es lo que usa `MexicoTemplateGeneratorTests`, que por eso vive en ese proyecto y no con los demás generadores. Un diseño con mucho texto puede no caber en los 15 KB junto con la matrícula: en México se quitaron el texto de tamaño 8 (ilegible en el coche) y el contorno del título, y el arte detallado se simplifica una vez al generar la plantilla (tolerancia 1) para que el presupuesto quede para los caracteres. Y si la fuente de la matrícula no tiene un guion utilizable (el `-` de LICENSE PLATE USA es una señal de "prohibido el paso"), los guiones van como arte fijo y el campo se pinta en trozos con `CharStart`/`CharCount` que se los saltan.

Japón (`JapanTemplateGeneratorTests`) reúne varias piezas pensadas para casos así:

- **`DigitPadChar`**: hace de un campo un número alineado a la derecha, con las posiciones vacías rellenas (`・・12`) y el guion solo cuando están los 4 dígitos.
- **`GlyphShapes`**: dibuja formas propias en lugar del glifo de la fuente para ciertos caracteres (su guion y su punto, que ninguna fuente disponible dibuja con las proporciones de la placa).
- **Retroceso por carácter en `WpfGlyphOutliner`**: si una fuente no tiene un carácter o lo tiene vacío, se toma de la siguiente fuente de la lista, en vez de desaparecer.

Japón usa Yu Gothic Bold, que viene con Windows. La única réplica libre de su tipografía, FZナンバープレートゴシック, se probó y se descartó: contornos toscos (curvas facetadas), kanji y せ vacíos, y solo para uso personal.
- **Grosor en `FontResolver`**: un nombre como `"Yu Gothic Bold"` elige ese grosor de la fuente del sistema; si no se indica, la normal, no la primera que liste la familia.

El número de serie se pinta en cinco huecos fijos, cada uno con su propio `CharStart`/`CharCount`, para que un `・` o el hueco vacío del guion caigan donde los pone una matrícula real.

Al exportar, si un SVG no cabe ni sin decimales, `ExportEscalation` simplifica solo los trazados con curvas (contornos de texto, arte vectorizado), empezando por tolerancia 0.5: los rectángulos, polígonos y esquinas redondeadas no se tocan, porque simplificarlos apenas ahorra y los estropea (una esquina redondeada se volvía un chaflán y una barra fina de 1 unidad desaparecía).

La calibración de tamaño/posición se hace normalmente midiendo una foto real: altura de los caracteres y posición de la línea base como fracción de la altura interior de la placa (España y Portugal salieron ~70-78% y ~85-87%, notablemente consistentes entre países). Cuando el propio SVG trae elementos que fijan la rejilla — los puntos de Portugal, o su línea divisoria, que resultó medir exactamente el ancho del bloque de dígitos — esos mandan sobre cualquier proporción medida en foto. Y si el SVG ya está dibujado a escala real en mm (como el de UK, 520×111 ≈ el tamaño físico real), la especificación oficial del país (en UK, la de la DVLA: altura 79mm, ancho 50mm, espacio entre caracteres 11mm, entre grupos 33mm) da cifras exactas sin necesidad de medir ninguna foto.

Usa `mask "9999 AAA"` (posiciones fijas) solo cuando el formato real del país las fija de verdad, como España. Para todo lo demás, `X` (dígito o letra libre) es la opción por defecto: el objetivo es un creador de decals permisivo, no una validación tipo DGT/DVLA.

## Hoja de ruta

- Más países en License Plate Generator (cada uno es una plantilla JSON nueva, más su propia tipografía — sin cambios de arquitectura).
- SVG Generator: editar/mover/escalar elementos individuales del SVG importado, no solo verlo; paleta de colores editable tras vectorizar.
- Editor de canvas más completo en general: panel de capas, selección de elementos.

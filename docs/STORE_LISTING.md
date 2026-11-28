# Ficha de Microsoft Store — textos e imágenes

Textos listos para pegar en Partner Center (*Store listings*) en **Español** e **Inglés**, y los prompts para generar el arte promocional con un generador de imágenes (ChatGPT / DALL·E).

Las fichas de los 10 idiomas (incluidos estos dos, con las novedades de la versión actual) están en [docs/store-listing/](store-listing/), un JSON por idioma con descripción, novedades, características, palabras clave y leyendas.

Límites de Partner Center: descripción ≤ 10 000 caracteres · *What's new* ≤ 1 500 · hasta 20 características de ≤ 200 · hasta 7 términos de búsqueda (≤ 30 caracteres c/u, ≤ 21 palabras en total) · leyenda de captura ≤ 200.

---

## 1. Español (es-ES)

**Nombre del producto:** PolyglotCLI

**Título corto:** PolyglotCLI

**Descripción**

```
PolyglotCLI traduce documentos completos —PDF, Word (DOCX/DOC), OpenDocument (ODT) y texto plano— usando modelos de inteligencia artificial que usted elige: modelos locales en su propio equipo (LM Studio u Ollama) o proveedores en la nube como OpenAI o Google Gemini.

Pensado para libros, manuales y documentos largos, PolyglotCLI procesa página por página y guarda cada resultado al instante. Si cierra la aplicación o cancela un trabajo, puede reanudarlo más tarde exactamente donde quedó, sin volver a traducir (ni volver a pagar) las páginas ya terminadas.

¿Documentos escaneados? Active el modo imagen: cada página se renderiza y un modelo con visión hace el OCR antes de traducir, conservando títulos, listas y párrafos.

PRIVACIDAD PRIMERO
Con un modelo local, sus documentos nunca salen de su equipo. No hay cuentas, ni telemetría, ni servidores propios: los documentos solo se envían al proveedor de IA que usted configure, y las claves de API se guardan cifradas en su equipo.

CONTROL TOTAL DEL RESULTADO
• Verificador de páginas: compare la página original con el OCR y la traducción lado a lado, y corrija lo que haga falta.
• Revisión opcional: un segundo paso de IA pule la traducción.
• Editor de prompts: ajuste las instrucciones de OCR, traducción y revisión a su estilo o glosario.
• Exporte a Markdown, Word (DOCX), PDF u OpenDocument (ODT).
• Historial de trabajos con progreso, reanudación y paquetes exportables (.zpg) para continuar en otro equipo.

Requisitos: para traducir necesita un modelo de IA, ya sea local (LM Studio u Ollama, gratuitos) o una clave de API de un proveedor en la nube. La interfaz está disponible en 10 idiomas y sigue automáticamente el idioma de Windows.

PolyglotCLI es software de código abierto: https://github.com/FittyAr/PolyglotCLI
```

**Novedades de esta versión (What's new)**

```
• Primera versión en Microsoft Store.
• Al reanudar trabajos de texto ya no se vuelven a traducir las páginas terminadas.
• Cancelar un trabajo detiene también las peticiones en curso al modelo de IA.
• Exportación a ODT integrada y más fiable.
• Importación de paquetes .zpg grandes (hasta 2 GB) más segura.
• Los paquetes exportados ya no incluyen claves de API.
```

**Características del producto**

```
Traduce PDF, Word (DOCX/DOC), OpenDocument (ODT) y texto plano
OCR con modelos de visión para documentos escaneados
Funciona con modelos locales (LM Studio, Ollama) o en la nube (OpenAI, Gemini)
Privacidad: con un modelo local sus documentos nunca salen del equipo
Traducción página por página con guardado incremental
Reanuda trabajos interrumpidos sin volver a traducir lo ya hecho
Verificador de páginas: original, OCR y traducción lado a lado
Revisión opcional de la traducción con un segundo paso de IA
Editor de prompts para OCR, traducción y revisión
Exporta a Markdown, DOCX, PDF y ODT
Historial de trabajos y paquetes exportables para cambiar de equipo
Claves de API cifradas en el equipo; sin cuentas ni telemetría
```

**Requisitos adicionales del sistema**

```
Mínimo: Windows 10 versión 1809 o posterior (x64), 4 GB de RAM.
Recomendado: 8 GB de RAM o más. Para usar modelos de IA locales se recomiendan 16 GB de RAM y una GPU dedicada.
Se necesita un modelo de IA: LM Studio u Ollama en el equipo, o una clave de API de un proveedor en la nube (OpenAI, Google Gemini u otro compatible con la API de OpenAI).
```

**Términos de búsqueda** (7)

```
traductor de documentos
traducir PDF
OCR
inteligencia artificial
LM Studio
Ollama
traducción
```

**Leyendas de capturas** (archivos en `assets/store/screenshots/`, 1920×1080, en este orden)

1. `Verificador de páginas: compare la página original con la traducción y corrija lo que necesite.`
2. `Panel principal: elija los documentos, el modo (texto u OCR) y siga el progreso en tiempo real.`
3. `Archivos generados: la traducción lista en Markdown y Word, con vista previa integrada.`
4. `Configure proveedores locales o en la nube y el modelo de IA para cada etapa.`
5. `Editor de prompts: adapte las instrucciones de la IA a su estilo y glosario.`
6. `Historial de trabajos: progreso, reanudación y exportación de cada traducción.`

**Copyright y marca:** `© 2026 FittyAr. Licencia MIT.`

**Desarrollado por:** `FittyAr`

**Sitio web:** `https://github.com/FittyAr/PolyglotCLI`

**Contacto de soporte:** `https://github.com/FittyAr/PolyglotCLI/issues`

**Política de privacidad:** `https://github.com/FittyAr/PolyglotCLI/blob/master/docs/PRIVACY.md`

---

## 2. English (en-US)

**Product name:** PolyglotCLI

**Description**

```
PolyglotCLI translates entire documents —PDF, Word (DOCX/DOC), OpenDocument (ODT) and plain text— using the AI models you choose: local models running on your own PC (LM Studio or Ollama) or cloud providers such as OpenAI or Google Gemini.

Built for books, manuals and long documents, PolyglotCLI works page by page and saves every result immediately. If you close the app or cancel a job, you can resume it later exactly where it stopped, without translating (or paying for) finished pages again.

Scanned documents? Switch to image mode: each page is rendered and a vision model performs OCR before translating, keeping headings, lists and paragraphs.

PRIVACY FIRST
With a local model, your documents never leave your PC. No accounts, no telemetry, no servers of our own: documents are only sent to the AI provider you configure, and API keys are stored encrypted on your device.

FULL CONTROL OVER THE RESULT
• Page verifier: compare the original page with the OCR and the translation side by side, and fix anything you need.
• Optional review: a second AI pass polishes the translation.
• Prompt editor: tune the OCR, translation and review instructions to your style or glossary.
• Export to Markdown, Word (DOCX), PDF or OpenDocument (ODT).
• Job history with progress, resume and exportable packages (.zpg) to continue on another PC.

Requirements: translating requires an AI model, either local (LM Studio or Ollama, both free) or an API key from a cloud provider. The user interface is available in 10 languages and automatically follows your Windows language.

PolyglotCLI is open-source software: https://github.com/FittyAr/PolyglotCLI
```

**What's new in this version**

```
• First release on the Microsoft Store.
• Resuming text-based jobs no longer re-translates finished pages.
• Cancelling a job now also stops in-flight requests to the AI model.
• Built-in, more reliable ODT export.
• Safer import of large .zpg packages (up to 2 GB).
• Exported packages no longer include API keys.
```

**Product features**

```
Translates PDF, Word (DOCX/DOC), OpenDocument (ODT) and plain text
OCR with vision models for scanned documents
Works with local models (LM Studio, Ollama) or the cloud (OpenAI, Gemini)
Privacy: with a local model your documents never leave your PC
Page-by-page translation with incremental saving
Resumes interrupted jobs without re-translating finished pages
Page verifier: original, OCR and translation side by side
Optional second AI pass to review the translation
Prompt editor for OCR, translation and review
Exports to Markdown, DOCX, PDF and ODT
Job history and exportable packages to move between PCs
API keys encrypted on your device; no accounts or telemetry
```

**Additional system requirements**

```
Minimum: Windows 10 version 1809 or later (x64), 4 GB RAM.
Recommended: 8 GB RAM or more. 16 GB RAM and a dedicated GPU are recommended for local AI models.
An AI model is required: LM Studio or Ollama on your PC, or an API key from a cloud provider (OpenAI, Google Gemini or any OpenAI-compatible API).
```

**Search terms** (7)

```
document translator
translate PDF
OCR
AI translation
LM Studio
Ollama
translation
```

**Screenshot captions**

1. `Page verifier: compare the original page with the translation and fix what you need.`
2. `Dashboard: pick your documents and mode (text or OCR), and follow progress in real time.`
3. `Generated files: your translation ready as Markdown and Word, with a built-in preview.`
4. `Set up local or cloud providers and the AI model for each stage.`
5. `Prompt editor: adapt the AI instructions to your style and glossary.`
6. `Job history: progress, resume and export for every translation.`

**Copyright and trademark info:** `© 2026 FittyAr. MIT License.`

**Developed by:** `FittyAr`

---

## 3. Imágenes para generar con ChatGPT

Paleta de la marca: fondo azul noche/índigo muy oscuro (`#0b0f19` → `#1e1b4b`), acentos violeta (`#6366f1`, `#8b5cf6`) y cian neón (`#22d3ee`), estilo de líneas de neón con brillo suave. El logo es una "P" de neón violeta-cian cuyas curvas se abren como hojas de papel en abanico, con símbolos de terminal `>>_`.

Generá cada imagen y pasame el archivo tal cual sale: yo la recorto/escalo al tamaño exacto que pide Partner Center (no hace falta pedirle a ChatGPT medidas exactas). Para la imagen 3 adjuntá también `assets/brand/Square.png` en el chat para que respete el logo.

### Imagen 1 — 16:9 Super hero art (1920×1080 / 3840×2160) — **la más importante**

Va arriba de todo en la ficha. Reglas de Microsoft: **sin texto, sin logos, sin capturas de la interfaz**, lo importante en el centro y nada clave en el tercio inferior.

```
Create a wide 16:9 landscape illustration (horizontal, cinematic) for a Microsoft Store hero banner of a desktop app that translates documents with AI.

Scene: in the center, a stack of glowing paper document pages fans out like a bird's wing, flowing from left to right. On the left side the pages carry abstract, unreadable glyph-like lines in warm violet light; as they move right they transform through a luminous stream of particles into pages with abstract lines in cool cyan light — visually suggesting one language turning into another. Subtle hints of a scanner light beam passing over one page (OCR) and faint circuit-like neural network filaments in the background.

Style: premium, modern, minimal neon line-art with soft glow and depth of field, dark night-blue / deep indigo background (#0b0f19 to #1e1b4b), accents in violet (#8b5cf6, #6366f1) and neon cyan (#22d3ee). Clean composition, key elements centered and in the upper two thirds; the bottom third should be calm, dark and empty.

Strict rules: absolutely NO text, NO letters, NO readable words, NO numbers, NO logos, NO flags, NO user interface, NO screens, NO devices, NO people, NO hands. Highest resolution available.
```

### Imagen 2 — 2:3 Poster art (720×1080 / 1440×2160) — recomendada

Se usa en colecciones y resultados de búsqueda. Puede llevar el nombre de la app, pero solo en los dos tercios superiores.

```
Create a vertical 2:3 portrait poster (taller than wide) for a desktop app called "PolyglotCLI" that translates documents with artificial intelligence.

Composition: in the upper half, a large glowing neon letter "P" made of violet-to-cyan light lines whose curves open into a fan of paper sheets, like pages turning in the wind, with a small terminal prompt symbol ">_" inside it. Around it, a few floating document pages with abstract unreadable lines dissolve into light particles. Directly below the emblem, in the upper two thirds of the image, the word "PolyglotCLI" in a clean, modern geometric sans-serif typeface, white with a soft cyan glow, perfectly spelled, high contrast and easy to read.

Style: premium neon line-art, soft glow, dark night-blue / deep indigo background (#0b0f19 to #1e1b4b), violet (#8b5cf6) and neon cyan (#22d3ee) accents. The bottom third must stay dark, calm and empty — no text or important elements there.

Strict rules: the only text allowed is "PolyglotCLI" spelled exactly like that. No other words, no slogans, no flags, no user interface, no devices, no people. Highest resolution available.
```

### Imagen 3 — 1:1 Box art (1080×1080 / 2160×2160) — opcional

*(Adjuntar `assets/brand/Square.png`.)*

```
Using the attached logo as the exact reference for the emblem (keep its shape, colors and style), create a square 1:1 promotional artwork: the neon "P" emblem centered slightly above the middle, larger and more detailed, with paper pages fanning out of it and dissolving into violet and cyan light particles that drift around it. Dark night-blue / deep indigo background (#0b0f19 to #1e1b4b) with a subtle radial glow behind the emblem.

Strict rules: NO text, NO letters, NO words, NO rounded-square app-icon frame, NO border, NO transparency, NO user interface. Keep the bottom third of the image calm and free of key elements. Highest resolution available.
```

> El **icono 1:1 de 300×300** (`assets/store/app-tile-300x300.png`) y los **logos del paquete** ya están generados a partir de `assets/brand`; no hace falta ChatGPT para eso. Las **capturas de pantalla** (`assets/store/screenshots/`) son de la app real con un documento de ejemplo de dominio público (*Alice's Adventures in Wonderland*, cap. 1).

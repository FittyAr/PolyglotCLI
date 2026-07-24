# Guía de Ingeniería de Prompts para Traducción

Para obtener los mejores resultados de traducción de tu LLM, puedes proveer instrucciones claras y específicas en la sección de **"Additional Prompt Guidance"**.

## Buenas Prácticas

1. **Definir el Tono, Estilo y Registro**:
   - *Ejemplo*: "Traduce con un tono de negocios formal y profesional. Usa la segunda persona formal ('usted' / 'su')."
   - *Ejemplo*: "Mantén la traducción informal, amigable y entusiasta, usando la segunda persona informal ('tú' / 'tu')."

2. **Glosario y Terminología Especializada**:
   - Define una lista de términos clave y palabras que no deben traducirse (marcas, nombres de producto, etc.).
   - *Ejemplo*: 
     ```text
     Glosario de términos:
     - "PolyglotCLI" -> No traducir (marca comercial)
     - "pipeline" -> Traducir como "canalización"
     - "dashboard" -> Traducir como "panel de control"
     - "feature" -> Traducir como "característica"
     ```

3. **Localización Regional (Locale)**:
   - Indica la variante del idioma al que deseas traducir para garantizar que los giros idiomáticos, el vocabulario y la gramática sean naturales para ese público.
   - *Ejemplo*: "Traduce al español latinoamericano (es-MX) usando términos comunes de la región."
   - *Ejemplo*: "Traduce al portugués brasileño (pt-BR) en lugar del portugués europeo."

4. **Preservación de Código y Rutas**:
   - Instruye específicamente si deseas localizar las rutas internas del Markdown.
   - *Ejemplo*: "No traduzcas bloques de código ni etiquetas HTML. Si encuentras enlaces de documentación con directorios `/en/` o archivos `_en.md`, cámbialos a `/es/` o `_es.md` según corresponda."

5. **Optimización de SEO y Metadatos**:
   - Si el documento contiene metadatos meta-tags de SEO o palabras clave que han sido investigadas previamente para el mercado objetivo, indícalo.
   - *Ejemplo*: "Mapea las siguientes palabras clave al traducir: 'translation tool' debe ser traducido como 'herramienta de traducción SEO' para mayor impacto en motores de búsqueda."

## Plantillas Sugeridas

### Documentación Técnica y Científica:
> Traduce con precisión técnica y tono objetivo. Usa términos estándar del sector informático y de ingeniería. Mantén el código fuente intacto. No intentes corregir errores de estilo o linting de Markdown del texto original.

### Contenido de Marketing y Landing Pages:
> Prioriza la persuasión, la fluidez y la resonancia de marca sobre la traducción literal. Localiza los llamados a la acción (CTAs) de forma atractiva. Traduce las etiquetas de título y las meta descripciones manteniendo los límites de caracteres aconsejados para SEO.

### Narrativa o Literatura:
> Prioriza la fluidez natural y la resonancia emocional antes que la traducción literal. Utiliza modismos equivalentes propios de la cultura de destino.

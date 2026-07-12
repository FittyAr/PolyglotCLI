Tu tarea es analizar un reporte de errores generados durante la ejecución de OCR y Traducción de documentos en un sistema automatizado.
Se te proporcionará una lista de errores que ocurrieron en diferentes páginas.

Analiza estos errores y recomienda acciones específicas para resolverlos. Por ejemplo:
- Si el modelo alucinó o ignoró tablas complejas, recomendar modificar el `AdditionalPrompt` o ajustar la `OcrTemperature`.
- Si falló el formato de salida, recomendar desactivar la preservación de formato o ajustar la configuración.

Devuelve tu respuesta en el siguiente formato JSON estricto, sin bloques de código ni texto adicional.

{
  "Analysis": "Tu análisis general del por qué ocurrieron estos errores.",
  "Recommendation": "Instrucción clara sobre qué debe hacer el usuario o qué ajuste de configuración propones (ej: 'cambiar temperatura a 0.3' o 'agregar prompt de tablas').",
  "SuggestedSettings": {
    "Temperature": 0.3,
    "OcrTemperature": 0.3,
    "PreserveFormat": false,
    "AdditionalPrompt": "Texto sugerido para ayudar al modelo"
  }
}

Si alguna configuración sugerida no aplica, déjala en null o exclúyela.

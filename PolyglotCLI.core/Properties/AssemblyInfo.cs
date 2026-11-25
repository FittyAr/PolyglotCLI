using System.Runtime.CompilerServices;

// Permite testear helpers internos (comparación de versiones del updater,
// resolución de proveedor por URL, saneo de config en paquetes) sin
// volverlos parte de la API pública.
[assembly: InternalsVisibleTo("PolyglotCLI.test")]

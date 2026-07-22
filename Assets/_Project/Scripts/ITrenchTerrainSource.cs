using UnityEngine;

namespace Excavator.Trench
{
    /// <summary>
    /// Interfaz desacoplada para fuentes de terreno en ejercicios de excavacion de zanja.
    /// Abstrae la representacion visual (Malla deformable, Terrain heightmap, Voxel, etc.)
    /// de la logica de evaluacion de dimensiones y volumen.
    /// </summary>
    public interface ITrenchTerrainSource
    {
        /// <summary>Numero de columnas en la grilla de muestreo espacial (Eje X).</summary>
        int GridCols { get; }

        /// <summary>Numero de filas en la grilla de muestreo espacial (Eje Z).</summary>
        int GridRows { get; }

        /// <summary>Ancho total del area de zanja en metros (Eje X).</summary>
        float AreaWidth { get; }

        /// <summary>Largo total del area de zanja en metros (Eje Z).</summary>
        float AreaLength { get; }

        /// <summary>Profundidad actual excavada (en metros positivos) en la celda (col, row).</summary>
        float GetDepthAt(int col, int row);

        /// <summary>Posicion en espacio de mundo de la celda (col, row).</summary>
        Vector3 GetCellWorldPosition(int col, int row);

        /// <summary>Restablece el terreno a su altura original (para reiniciar ejercicios).</summary>
        void ResetTerrain();
    }
}

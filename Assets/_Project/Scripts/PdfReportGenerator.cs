using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Excavator.Reporting
{
    /// <summary>
    /// Datos estructurados de un evento de colision / penalizacion.
    /// </summary>
    [Serializable]
    public class PenaltyEventData
    {
        public float timestampSeconds;
        public string timeFormatted;       // MM:SS
        public string objectName;          // Nombre del objeto o estructura
        public string penaltyCategory;     // P. ej. Barrera Rigida, Canalizador, Terreno
        public float impactSpeed;          // Velocidad relativa m/s
        public int scoreDeduction;         // Puntos restados
        public string severityLevel;       // Leve, Media, Grave
    }

    /// <summary>
    /// Datos completos de la sesion de evaluacion para la generacion del reporte.
    /// </summary>
    [Serializable]
    public class EvaluationReportData
    {
        public string operatorName = "Operador no registrado";
        public string operatorId = "OP-2026-001";
        public string evaluationDate = "";
        public string scenarioName = "Escenario 1 — Circuito de Maniobras y Estacionamiento";
        public float totalSessionTime;
        public string sessionTimeFormatted = "00:00";
        public int initialScore = 100;
        public int finalScore = 100;
        public string statusText = "APROBADO";
        public Color statusColor = Color.green;

        // Protocolo de Apagado / Checklist
        public bool condParked;
        public bool condArmGrounded;
        public bool condEngineOff;

        // Lista de penalizaciones
        public List<PenaltyEventData> penalties = new List<PenaltyEventData>();

        // Estadisticas
        public int totalCollisions;
        public int totalDeductions;
        public float maxImpactSpeed;
    }

    /// <summary>
    /// Generador de archivos PDF version 1.4 escrito en C# nativo (sin DLLs ni librerias externas).
    /// Genera documentos en formato A4 con encabezados industriales, tablas, metricas y seccion de firmas.
    /// </summary>
    public static class PdfReportGenerator
    {
        /// <summary>
        /// Genera y guarda un archivo PDF con el informe formal de evaluacion.
        /// Retorna la ruta absoluta del archivo generado.
        /// </summary>
        public static string GeneratePdfReport(EvaluationReportData data, string customDirectory = null)
        {
            try
            {
                string folder = string.IsNullOrEmpty(customDirectory)
                    ? Path.Combine(Directory.GetCurrentDirectory(), "Reportes")
                    : customDirectory;

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string safeName = SanitizeFileName(data.operatorName);
                string fileName = $"Reporte_Evaluacion_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string filePath = Path.Combine(folder, fileName);

                byte[] pdfBytes = BuildPdfDocument(data);
                File.WriteAllBytes(filePath, pdfBytes);

                Debug.Log($"[PdfReportGenerator] Reporte PDF generado exitosamente en: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PdfReportGenerator] Error al generar el PDF: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Operador";
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid) name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }

        // ------------------------------------------------------------------
        // CONSTRUCCIÓN DEL DOCUMENTO PDF 1.4 (BINARY BUILDER)
        // ------------------------------------------------------------------
        private static byte[] BuildPdfDocument(EvaluationReportData data)
        {
            List<byte[]> objects = new List<byte[]>();
            List<long> offsets = new List<long>();

            // Construir el stream de contenido de la pagina principal (A4: 595.28 x 841.89 pt)
            string pageContentStream = BuildPageContentStream(data);
            byte[] contentStreamBytes = Encoding.UTF8.GetBytes(pageContentStream);

            // Objeto 1: Catalog
            string obj1 = "1 0 obj\n<</Type /Catalog /Pages 2 0 R>>\nendobj\n";
            // Objeto 2: Pages
            string obj2 = "2 0 obj\n<</Type /Pages /Kids [3 0 R] /Count 1>>\nendobj\n";
            // Objeto 3: Page (A4: 595 x 842 pt)
            string obj3 = "3 0 obj\n<</Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] /Resources <</Font <</F1 4 0 R /F2 5 0 R /F3 6 0 R>>>> /Contents 7 0 R>>\nendobj\n";
            // Objeto 4: Font Helvetica (WinAnsiEncoding)
            string obj4 = "4 0 obj\n<</Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding>>\nendobj\n";
            // Objeto 5: Font Helvetica-Bold (WinAnsiEncoding)
            string obj5 = "5 0 obj\n<</Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding>>\nendobj\n";
            // Objeto 6: Font Helvetica-Oblique (WinAnsiEncoding)
            string obj6 = "6 0 obj\n<</Type /Font /Subtype /Type1 /BaseFont /Helvetica-Oblique /Encoding /WinAnsiEncoding>>\nendobj\n";

            // Objeto 7: Contents Stream
            string obj7Header = $"7 0 obj\n<</Length {contentStreamBytes.Length}>>\nstream\n";
            string obj7Footer = "\nendstream\nendobj\n";
            byte[] obj7HeaderBytes = Encoding.UTF8.GetBytes(obj7Header);
            byte[] obj7FooterBytes = Encoding.UTF8.GetBytes(obj7Footer);

            byte[] obj7Bytes = CombineByteArrays(obj7HeaderBytes, contentStreamBytes, obj7FooterBytes);

            objects.Add(Encoding.UTF8.GetBytes(obj1));
            objects.Add(Encoding.UTF8.GetBytes(obj2));
            objects.Add(Encoding.UTF8.GetBytes(obj3));
            objects.Add(Encoding.UTF8.GetBytes(obj4));
            objects.Add(Encoding.UTF8.GetBytes(obj5));
            objects.Add(Encoding.UTF8.GetBytes(obj6));
            objects.Add(obj7Bytes);

            // Armar archivo completo
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] header = Encoding.UTF8.GetBytes("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
                ms.Write(header, 0, header.Length);

                for (int i = 0; i < objects.Count; i++)
                {
                    offsets.Add(ms.Position);
                    ms.Write(objects[i], 0, objects[i].Length);
                }

                long xrefOffset = ms.Position;
                StringBuilder xref = new StringBuilder();
                xref.Append("xref\n");
                xref.Append($"0 {objects.Count + 1}\n");
                xref.Append("0000000000 65535 f \n");
                for (int i = 0; i < offsets.Count; i++)
                {
                    xref.Append($"{offsets[i]:D10} 00000 n \n");
                }
                xref.Append("trailer\n");
                xref.Append($"<</Size {objects.Count + 1} /Root 1 0 R>>\n");
                xref.Append("startxref\n");
                xref.Append($"{xrefOffset}\n");
                xref.Append("%%EOF\n");

                byte[] xrefBytes = Encoding.UTF8.GetBytes(xref.ToString());
                ms.Write(xrefBytes, 0, xrefBytes.Length);

                return ms.ToArray();
            }
        }

        private static byte[] CombineByteArrays(params byte[][] arrays)
        {
            int totalLen = 0;
            foreach (var a in arrays) totalLen += a.Length;
            byte[] res = new byte[totalLen];
            int offset = 0;
            foreach (var a in arrays)
            {
                Buffer.BlockCopy(a, 0, res, offset, a.Length);
                offset += a.Length;
            }
            return res;
        }

        // ------------------------------------------------------------------
        // DIBUJO DE CONTENIDO PDF (COMANDOS GRAPHICS / TEXT)
        // ------------------------------------------------------------------
        private static string BuildPageContentStream(EvaluationReportData data)
        {
            StringBuilder sb = new StringBuilder();

            // Helpers de coordenadas PDF (0,0 es abajo a la izquierda; Y va hasta 841.89)
            // Ancho A4: 595.28 pt, Alto A4: 841.89 pt. Margin izq: 36 pt, Margin der: 559 pt.
            float margin = 36f;
            float pageWidth = 595.28f;
            float topY = 841.89f - 36f; // 805.89 pt

            // --- 1. BANNER ENCABEZADO INSTITUCIONAL ---
            float bannerH = 54f;
            float bannerY = topY - bannerH;
            // Fondo Azul Industrial (#102A45)
            sb.AppendLine("0.06 0.16 0.27 rg"); // Set Fill RGB
            sb.AppendLine($"{margin} {bannerY} {pageWidth - (margin * 2)} {bannerH} re f");

            // Linea decorativa inferior en naranja industrial (#FF8C00)
            sb.AppendLine("1.00 0.55 0.00 rg");
            sb.AppendLine($"{margin} {bannerY - 3f} {pageWidth - (margin * 2)} 3 re f");

            // Texto Titulo Banner (Blanco, Helvetica-Bold 13pt)
            sb.AppendLine("BT");
            sb.AppendLine("/F2 13 Tf");
            sb.AppendLine("1 1 1 rg");
            sb.AppendLine($"{margin + 14f} {bannerY + 32f} Td");
            sb.AppendLine($"({EscapePdfText("CENTRO DE FORMACION Y SIMULACION DE MAQUINARIA PESADA")}) Tj");
            sb.AppendLine("ET");

            // Subtitulo Banner
            sb.AppendLine("BT");
            sb.AppendLine("/F1 9 Tf");
            sb.AppendLine("0.85 0.90 0.95 rg");
            sb.AppendLine($"{margin + 14f} {bannerY + 14f} Td");
            sb.AppendLine($"({EscapePdfText("INFORME TECNICO DE EVALUACION DE OPERADOR DE EXCAVADORA HIDRAULICA")}) Tj");
            sb.AppendLine("ET");

            float curY = bannerY - 20f;

            // --- 2. FICHA TÉCNICA DEL OPERADOR Y SESIÓN ---
            float boxW = pageWidth - (margin * 2);
            float cardH = 68f;
            curY -= cardH;

            // Marco Fondo Gris Claro (#F4F6F8)
            sb.AppendLine("0.95 0.96 0.97 rg");
            sb.AppendLine($"{margin} {curY} {boxW} {cardH} re f");
            sb.AppendLine("0.75 0.78 0.82 RG"); // Stroke color
            sb.AppendLine("0.8 w");
            sb.AppendLine($"{margin} {curY} {boxW} {cardH} re S");

            // Titulo Seccion Ficha
            sb.AppendLine("BT");
            sb.AppendLine("/F2 10 Tf");
            sb.AppendLine("0.10 0.15 0.22 rg");
            sb.AppendLine($"{margin + 10f} {curY + cardH - 16f} Td");
            sb.AppendLine($"({EscapePdfText("1. FICHA TECNICA DE LA EVALUACION")}) Tj");
            sb.AppendLine("ET");

            // Contenido 2 Columnas
            float col1X = margin + 10f;
            float col2X = margin + (boxW * 0.5f) + 10f;

            DrawPdfLabelValue(sb, col1X, curY + cardH - 32f, "Operador Evaluado:", data.operatorName);
            DrawPdfLabelValue(sb, col1X, curY + cardH - 46f, "ID / Legajo:", data.operatorId);
            DrawPdfLabelValue(sb, col1X, curY + cardH - 60f, "Fecha y Hora:", string.IsNullOrEmpty(data.evaluationDate) ? DateTime.Now.ToString("dd/MM/yyyy HH:mm") : data.evaluationDate);

            DrawPdfLabelValue(sb, col2X, curY + cardH - 32f, "Modulo / Escenario:", "Circuito de Maniobras y Estacionamiento");
            DrawPdfLabelValue(sb, col2X, curY + cardH - 46f, "Tiempo de Operacion:", data.sessionTimeFormatted);
            DrawPdfLabelValue(sb, col2X, curY + cardH - 60f, "Protocolo Apagado:", data.condEngineOff ? "Correcto (Motor Apagado)" : "Pendiente / Incumplido");

            curY -= 15f;

            // --- 3. RESULTADO DE EVALUACIÓN Y CALIFICACIÓN ---
            float resH = 50f;
            curY -= resH;

            // Color de estado segun puntaje
            float rScore = 0.1f, gScore = 0.65f, bScore = 0.25f; // Verde por defecto
            if (data.finalScore < 50)
            {
                rScore = 0.82f; gScore = 0.15f; bScore = 0.15f; // Rojo
            }
            else if (data.finalScore < 75)
            {
                rScore = 0.90f; gScore = 0.55f; bScore = 0.05f; // Naranja/Amarillo
            }

            // Fondo Resumen Calificacion
            sb.AppendLine($"{rScore * 0.12f + 0.88f:F2} {gScore * 0.12f + 0.88f:F2} {bScore * 0.12f + 0.88f:F2} rg");
            sb.AppendLine($"{margin} {curY} {boxW} {resH} re f");
            sb.AppendLine($"{rScore:F2} {gScore:F2} {bScore:F2} RG");
            sb.AppendLine("1.5 w");
            sb.AppendLine($"{margin} {curY} {boxW} {resH} re S");

            // Texto Puntaje Grande
            sb.AppendLine("BT");
            sb.AppendLine("/F2 20 Tf");
            sb.AppendLine($"{rScore:F2} {gScore:F2} {bScore:F2} rg");
            sb.AppendLine($"{margin + 16f} {curY + 18f} Td");
            sb.AppendLine($"({EscapePdfText($"{data.finalScore} / 100 PTS")}) Tj");
            sb.AppendLine("ET");

            // Badge / Dictamen
            sb.AppendLine("BT");
            sb.AppendLine("/F2 11 Tf");
            sb.AppendLine("0.10 0.10 0.12 rg");
            sb.AppendLine($"{margin + 170f} {curY + 28f} Td");
            sb.AppendLine($"({EscapePdfText($"DICTAMEN TECNICO: {data.statusText}")}) Tj");
            sb.AppendLine("ET");

            sb.AppendLine("BT");
            sb.AppendLine("/F1 8.5 Tf");
            sb.AppendLine("0.30 0.32 0.35 rg");
            sb.AppendLine($"{margin + 170f} {curY + 12f} Td");
            sb.AppendLine($"({EscapePdfText($"Total Colisiones: {data.totalCollisions}  |  Descuento Acumulado: -{data.totalDeductions} pts  |  Vel. Max Impacto: {data.maxImpactSpeed:F2} m/s")}) Tj");
            sb.AppendLine("ET");

            curY -= 15f;

            // --- 4. AUDITORÍA DE PROTOCOLO DE SEGURIDAD Y ESTACIONAMIENTO ---
            float protoH = 42f;
            curY -= protoH;

            sb.AppendLine("0.98 0.98 0.99 rg");
            sb.AppendLine($"{margin} {curY} {boxW} {protoH} re f");
            sb.AppendLine("0.82 0.84 0.88 RG");
            sb.AppendLine("0.8 w");
            sb.AppendLine($"{margin} {curY} {boxW} {protoH} re S");

            sb.AppendLine("BT");
            sb.AppendLine("/F2 9.5 Tf");
            sb.AppendLine("0.15 0.20 0.28 rg");
            sb.AppendLine($"{margin + 10f} {curY + protoH - 14f} Td");
            sb.AppendLine($"({EscapePdfText("2. AUDITORIA DE PROTOCOLO FINAL DE SEGURIDAD")}) Tj");
            sb.AppendLine("ET");

            float checkY = curY + 10f;
            DrawCheckmarkStatus(sb, margin + 15f, checkY, "1. Posicionamiento en Zona:", data.condParked);
            DrawCheckmarkStatus(sb, margin + 190f, checkY, "2. Pala Apoyada en Suelo:", data.condArmGrounded);
            DrawCheckmarkStatus(sb, margin + 370f, checkY, "3. Apagado de Motor:", data.condEngineOff);

            curY -= 20f;

            // --- 5. REGISTRO DETALLADO DE PENALIZACIONES E INCIDENCIAS ---
            sb.AppendLine("BT");
            sb.AppendLine("/F2 10 Tf");
            sb.AppendLine("0.10 0.15 0.22 rg");
            sb.AppendLine($"{margin} {curY} Td");
            sb.AppendLine($"({EscapePdfText("3. REGISTRO DETALLADO DE INFRACCIONES Y COLISIONES")}) Tj");
            sb.AppendLine("ET");

            curY -= 6f;

            // Encabezado de Tabla
            float tableHeaderH = 18f;
            curY -= tableHeaderH;
            sb.AppendLine("0.17 0.24 0.31 rg"); // Header Azul Oscuro
            sb.AppendLine($"{margin} {curY} {boxW} {tableHeaderH} re f");

            sb.AppendLine("BT");
            sb.AppendLine("/F2 8.5 Tf");
            sb.AppendLine("1 1 1 rg");
            sb.AppendLine($"{margin + 8f} {curY + 5f} Td"); sb.AppendLine($"({EscapePdfText("#")}) Tj");
            sb.AppendLine($"{margin + 28f} {curY + 5f} Td"); sb.AppendLine($"({EscapePdfText("TIEMPO")}) Tj");
            sb.AppendLine($"{margin + 85f} {curY + 5f} Td"); sb.AppendLine($"({EscapePdfText("ELEMENTO IMPACTADO")}) Tj");
            sb.AppendLine($"{margin + 245f} {curY + 5f} Td"); sb.AppendLine($"({EscapePdfText("CATEGORIA DE FALTA")}) Tj");
            sb.AppendLine($"{margin + 395f} {curY + 5f} Td"); sb.AppendLine($"({EscapePdfText("VELOCIDAD")}) Tj");
            sb.AppendLine($"{margin + 465f} {curY + 5f} Td"); sb.AppendLine($"({EscapePdfText("PENALIZACION")}) Tj");
            sb.AppendLine("ET");

            // Filas de Penalidades
            float rowH = 16f;
            int maxRows = 9; // Limite por pagina A4
            int displayedRows = Math.Min(data.penalties.Count, maxRows);

            if (displayedRows == 0)
            {
                // Fila Vacia - Sin Infracciones
                curY -= rowH;
                sb.AppendLine("0.96 0.98 0.96 rg");
                sb.AppendLine($"{margin} {curY} {boxW} {rowH} re f");
                sb.AppendLine("0.80 0.82 0.85 RG");
                sb.AppendLine("0.5 w");
                sb.AppendLine($"{margin} {curY} {boxW} {rowH} re S");

                sb.AppendLine("BT");
                sb.AppendLine("/F3 8.5 Tf");
                sb.AppendLine("0.10 0.50 0.20 rg");
                sb.AppendLine($"{margin + 12f} {curY + 4f} Td");
                sb.AppendLine($"({EscapePdfText("Sin infracciones registradas. La operacion se ejecuto con total limpieza y control de seguridad.")}) Tj");
                sb.AppendLine("ET");
            }
            else
            {
                for (int i = 0; i < displayedRows; i++)
                {
                    var p = data.penalties[i];
                    curY -= rowH;

                    // Alternar color de fondo
                    if (i % 2 == 0)
                        sb.AppendLine("1.0 1.0 1.0 rg");
                    else
                        sb.AppendLine("0.96 0.97 0.98 rg");

                    sb.AppendLine($"{margin} {curY} {boxW} {rowH} re f");
                    sb.AppendLine("0.85 0.87 0.90 RG");
                    sb.AppendLine("0.4 w");
                    sb.AppendLine($"{margin} {curY} {boxW} {rowH} re S");

                    // Texto Fila
                    sb.AppendLine("BT");
                    sb.AppendLine("/F1 8 Tf");
                    sb.AppendLine("0.15 0.15 0.18 rg");

                    sb.AppendLine($"{margin + 8f} {curY + 4f} Td");  sb.AppendLine($"({EscapePdfText((i + 1).ToString())}) Tj");
                    sb.AppendLine($"{margin + 28f} {curY + 4f} Td"); sb.AppendLine($"({EscapePdfText(p.timeFormatted)}) Tj");
                    sb.AppendLine($"{margin + 85f} {curY + 4f} Td"); sb.AppendLine($"({EscapePdfText(TruncateString(p.objectName, 32))}) Tj");
                    sb.AppendLine($"{margin + 245f} {curY + 4f} Td"); sb.AppendLine($"({EscapePdfText(TruncateString(p.penaltyCategory, 28))}) Tj");
                    sb.AppendLine($"{margin + 395f} {curY + 4f} Td"); sb.AppendLine($"({EscapePdfText($"{p.impactSpeed:F2} m/s")}) Tj");

                    // Resaltar penalidad en rojo si es grave
                    if (p.scoreDeduction >= 15 || p.severityLevel == "Grave")
                        sb.AppendLine("0.80 0.10 0.10 rg");
                    else
                        sb.AppendLine("0.70 0.35 0.00 rg");

                    sb.AppendLine($"{margin + 465f} {curY + 4f} Td"); sb.AppendLine($"({EscapePdfText($"-{p.scoreDeduction} pts")}) Tj");
                    sb.AppendLine("ET");
                }

                if (data.penalties.Count > maxRows)
                {
                    curY -= 12f;
                    sb.AppendLine("BT");
                    sb.AppendLine("/F3 7.5 Tf");
                    sb.AppendLine("0.45 0.45 0.48 rg");
                    sb.AppendLine($"{margin + 8f} {curY} Td");
                    sb.AppendLine($"({EscapePdfText($"... y {data.penalties.Count - maxRows} infraccion(es) mas registradas en telemetria.")}) Tj");
                    sb.AppendLine("ET");
                }
            }

            curY -= 18f;

            // --- 6. DIAGNÓSTICO TÉCNICO Y OBSERVACIONES AUTOMÁTICAS ---
            float diagH = 54f;
            curY -= diagH;

            sb.AppendLine("0.96 0.96 0.98 rg");
            sb.AppendLine($"{margin} {curY} {boxW} {diagH} re f");
            sb.AppendLine("0.78 0.80 0.85 RG");
            sb.AppendLine("0.8 w");
            sb.AppendLine($"{margin} {curY} {boxW} {diagH} re S");

            sb.AppendLine("BT");
            sb.AppendLine("/F2 9 Tf");
            sb.AppendLine("0.10 0.15 0.25 rg");
            sb.AppendLine($"{margin + 10f} {curY + diagH - 14f} Td");
            sb.AppendLine($"({EscapePdfText("4. OBSERVACIONES Y EVALUACION TECNICA DEL INSTRUCTOR")}) Tj");
            sb.AppendLine("ET");

            string diagLine1, diagLine2;
            GenerateDiagnosticComments(data, out diagLine1, out diagLine2);

            sb.AppendLine("BT");
            sb.AppendLine("/F1 8 Tf");
            sb.AppendLine("0.20 0.22 0.26 rg");
            sb.AppendLine($"{margin + 10f} {curY + diagH - 28f} Td");
            sb.AppendLine($"({EscapePdfText(diagLine1)}) Tj");
            sb.AppendLine($"{margin + 10f} {curY + diagH - 42f} Td");
            sb.AppendLine($"({EscapePdfText(diagLine2)}) Tj");
            sb.AppendLine("ET");

            curY -= 25f;

            // --- 7. BLOQUE DE FIRMAS FORMALES ---
            float sigW = (boxW - 40f) * 0.5f;

            // Linea Firma Evaluador
            sb.AppendLine("0.40 0.42 0.45 RG");
            sb.AppendLine("1.0 w");
            sb.AppendLine($"{margin} {curY} {sigW} 0 re S");

            sb.AppendLine("BT");
            sb.AppendLine("/F2 8.5 Tf");
            sb.AppendLine("0.15 0.18 0.22 rg");
            sb.AppendLine($"{margin} {curY - 12f} Td");
            sb.AppendLine($"({EscapePdfText("Firma del Evaluador / Instructor Tecnico")}) Tj");
            sb.AppendLine("ET");

            sb.AppendLine("BT");
            sb.AppendLine("/F1 7.5 Tf");
            sb.AppendLine("0.45 0.48 0.52 rg");
            sb.AppendLine($"{margin} {curY - 22f} Td");
            sb.AppendLine($"({EscapePdfText("Aclaracion, Cargo y Registro Profesional")}) Tj");
            sb.AppendLine("ET");

            // Linea Firma Operador
            float sig2X = margin + sigW + 40f;
            sb.AppendLine($"{sig2X} {curY} {sigW} 0 re S");

            sb.AppendLine("BT");
            sb.AppendLine("/F2 8.5 Tf");
            sb.AppendLine("0.15 0.18 0.22 rg");
            sb.AppendLine($"{sig2X} {curY - 12f} Td");
            sb.AppendLine($"({EscapePdfText("Firma del Operador Evaluado")}) Tj");
            sb.AppendLine("ET");

            sb.AppendLine("BT");
            sb.AppendLine("/F1 7.5 Tf");
            sb.AppendLine("0.45 0.48 0.52 rg");
            sb.AppendLine($"{sig2X} {curY - 22f} Td");
            sb.AppendLine($"({EscapePdfText("Conformidad de Resultados y Notificacion")}) Tj");
            sb.AppendLine("ET");

            // Pie de Pagina Sistema
            sb.AppendLine("BT");
            sb.AppendLine("/F3 7 Tf");
            sb.AppendLine("0.55 0.58 0.62 rg");
            sb.AppendLine($"{margin} 24 Td");
            sb.AppendLine($"({EscapePdfText($"Documento generado por Simulador Industrial de Excavadora UNAHUR v2.0 - ID Sesion: {Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}")}) Tj");
            sb.AppendLine("ET");

            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // HELPERS DE RELEVAMIENTO DE TEXTO Y DIAGNÓSTICO
        // ------------------------------------------------------------------
        private static void DrawPdfLabelValue(StringBuilder sb, float x, float y, string label, string val)
        {
            sb.AppendLine("BT");
            sb.AppendLine("/F2 8 Tf");
            sb.AppendLine("0.35 0.38 0.42 rg");
            sb.AppendLine($"{x} {y} Td");
            sb.AppendLine($"({EscapePdfText(label)}) Tj");
            sb.AppendLine("ET");

            float labelWidthEst = label.Length * 4.6f;
            sb.AppendLine("BT");
            sb.AppendLine("/F1 8.5 Tf");
            sb.AppendLine("0.10 0.12 0.15 rg");
            sb.AppendLine($"{x + labelWidthEst + 4f} {y} Td");
            sb.AppendLine($"({EscapePdfText(val)}) Tj");
            sb.AppendLine("ET");
        }

        private static void DrawCheckmarkStatus(StringBuilder sb, float x, float y, string label, bool isOk)
        {
            string symbol = isOk ? "[ OK ]" : "[ X ]";
            float r = isOk ? 0.05f : 0.80f;
            float g = isOk ? 0.60f : 0.15f;
            float b = isOk ? 0.15f : 0.15f;

            sb.AppendLine("BT");
            sb.AppendLine("/F2 8.5 Tf");
            sb.AppendLine($"{r:F2} {g:F2} {b:F2} rg");
            sb.AppendLine($"{x} {y} Td");
            sb.AppendLine($"({EscapePdfText(symbol)}) Tj");
            sb.AppendLine("ET");

            sb.AppendLine("BT");
            sb.AppendLine("/F1 8 Tf");
            sb.AppendLine("0.20 0.22 0.25 rg");
            sb.AppendLine($"{x + 32f} {y} Td");
            sb.AppendLine($"({EscapePdfText(label)}) Tj");
            sb.AppendLine("ET");
        }

        private static void GenerateDiagnosticComments(EvaluationReportData d, out string line1, out string line2)
        {
            if (d.finalScore >= 90)
            {
                line1 = "Desempeno operacional sobresaliente. Excelente nocion espacial del equipo y respeto a las zonas de seguridad.";
                line2 = "Se recomienda habilitar al operador para maniobras de mayor complejidad o ciclos de produccion intensivos.";
            }
            else if (d.finalScore >= 75)
            {
                line1 = "Conduccion satisfactoria dentro de los parametros de seguridad reglamentarios. Se registraron roces menores.";
                line2 = "Se sugiere reforzar el control de distancias laterales al maniobrar cerca de estructuras o barreras perimetrales.";
            }
            else if (d.finalScore >= 50)
            {
                line1 = "Operacion con deficiencias de precision. Multiples colisiones con elementos de senalizacion o barreras.";
                line2 = "Requiere practica guiada de giro de superestructura y control de velocidad en pasillos reducidos.";
            }
            else
            {
                line1 = "NIVEL DE RIESGO ELEVADO. Exceso de colisiones graves o severas contra estructuras y equipos viales.";
                line2 = "REPROBADO. Obligatorio completar modulo teorico de seguridad y repetir circuito de maniobras basicas.";
            }
        }

        private static string TruncateString(string str, int maxLen)
        {
            if (string.IsNullOrEmpty(str)) return "";
            if (str.Length <= maxLen) return str;
            return str.Substring(0, maxLen - 3) + "...";
        }

        private static string EscapePdfText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
                       .Replace("Á", "A").Replace("É", "E").Replace("Í", "I").Replace("Ó", "O").Replace("Ú", "U")
                       .Replace("ñ", "n").Replace("Ñ", "N").Replace("°", " deg");

            StringBuilder sb = new StringBuilder();
            foreach (char c in text)
            {
                if (c == '(' || c == ')' || c == '\\')
                    sb.Append('\\').Append(c);
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }
    }
}

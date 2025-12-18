using System;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CapaPresentacion // <--- ASEGÚRATE QUE ESTE NAMESPACE SEA EL TUYO
{
    public class InstaladorDB
    {
        public static void GestionarInstalacion()
        {
            // ------------------ CONFIGURACIÓN ------------------
            string nombreBaseDatos = "Carpinteria"; // <--- CAMBIA ESTO POR TU NOMBRE DE BD REAL
            string nombreRecurso = "CapaPresentacion.Instalacion.script_instalacion.sql"; // <--- VERIFICA ESTO
            // ----------------------------------------------------

            // 1. DETECTAR SERVIDOR (La parte nueva "Inteligente")
            string servidor = ObtenerServidorFuncional();

            if (servidor == null)
            {
                MessageBox.Show("No se encontró ningún SQL Server (ni Express ni Local). \nAsegúrate de tener SQL Server instalado.", "Error Crítico", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Cadenas de conexión dinámicas según el servidor encontrado
            string cadenaMaster = $"Server={servidor};Database=master; Integrated Security=True";
            string cadenaTuBD = $"Server={servidor};Database={nombreBaseDatos}; Integrated Security=True";

            try
            {
                bool existe = false;
                using (SqlConnection con = new SqlConnection(cadenaMaster))
                {
                    con.Open();
                    string query = $"SELECT database_id FROM sys.databases WHERE Name = '{nombreBaseDatos}'";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        existe = (cmd.ExecuteScalar() != null);
                    }
                }

                if (!existe)
                {
                    CrearBaseDeDatos(cadenaMaster, cadenaTuBD, nombreBaseDatos, nombreRecurso);
                    // Solo mostramos mensaje si realmente la instaló
                    // MessageBox.Show("¡Base de datos instalada correctamente!", "Sistema", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al verificar/instalar la base de datos:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --- MÉTODO NUEVO: Prueba cuál servidor tienes ---
        private static string ObtenerServidorFuncional()
        {
            string[] opciones = { ".\\SQLEXPRESS", "." }; // Probamos Express y luego Local

            foreach (string opcion in opciones)
            {
                try
                {
                    string cadenaTest = $"Server={opcion};Database=master; Integrated Security=True";
                    using (SqlConnection con = new SqlConnection(cadenaTest))
                    {
                        con.Open(); // Si abre, este es el ganador
                        return opcion;
                    }
                }
                catch { /* Si falla, sigue con el siguiente */ }
            }
            return null; // No funcionó ninguno
        }

        private static void CrearBaseDeDatos(string cadenaMaster, string cadenaTuBD, string nombreBD, string recurso)
        {
            using (SqlConnection con = new SqlConnection(cadenaMaster))
            {
                con.Open();
                string queryCrear = $"CREATE DATABASE [{nombreBD}]";
                using (SqlCommand cmd = new SqlCommand(queryCrear, con))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(recurso))
            {
                if (stream == null)
                {
                    // Si falla, mostramos los nombres disponibles para ayudar a corregir
                    string disponibles = string.Join("\n", assembly.GetManifestResourceNames());
                    throw new Exception($"No se halló el script: '{recurso}'.\nDisponibles:\n{disponibles}");
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    string scriptCompleto = reader.ReadToEnd();
                    string[] comandos = Regex.Split(scriptCompleto, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

                    using (SqlConnection con = new SqlConnection(cadenaTuBD))
                    {
                        con.Open();
                        foreach (string comando in comandos)
                        {
                            if (!string.IsNullOrWhiteSpace(comando))
                            {
                                using (SqlCommand cmd = new SqlCommand(comando, con))
                                {
                                    try { cmd.ExecuteNonQuery(); } catch { }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
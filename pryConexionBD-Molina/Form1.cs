using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Data.OleDb;

namespace pryConexionBD_Molina
{
    public partial class Form1 : Form
    {
        private string selectedFilePath = null;

        public Form1()
        {
            InitializeComponent();
            // Set a default connection string (example for SQL Server LocalDB)
            txtConnectionString.Text = "Server=(localdb)\\mssqllocaldb;Integrated Security=true;Initial Catalog=MiBase;";
        }

        private void btnExaminar_Click(object sender, EventArgs e)
        {
            openFileDialog1.InitialDirectory = Application.StartupPath; // ← ajusta esto
            openFileDialog1.Filter = "SQL Server Database|*.mdf|All Files|*.*";
            openFileDialog1.Title = "Seleccionar Base de Datos";
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;

            selectedFilePath = openFileDialog1.FileName;
            lblArchivo.Text = "Archivo: " + Path.GetFileName(selectedFilePath);
            btnMostrar.Enabled = true;

            // Build a sample connection string using the file name (for demonstration)
            // Here we just put the file name as Database name in the connection string
            string dbName = Path.GetFileNameWithoutExtension(selectedFilePath);
            txtConnectionString.Text = $"Server=(localdb)\\mssqllocaldb;Integrated Security=true;Initial Catalog={dbName};";
        }

        private void btnMostrar_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFilePath)) return;

            var ext = Path.GetExtension(selectedFilePath).ToLowerInvariant();

            try
            {
                if (ext == ".accdb" || ext == ".mdb")
                {
                    // Build Access connection string
                    string provider = ext == ".accdb" ? "Microsoft.ACE.OLEDB.12.0" : "Microsoft.Jet.OLEDB.4.0";
                    string connStr = $"Provider={provider};Data Source={selectedFilePath};Persist Security Info=False;";
                    txtConnectionString.Text = connStr;

                    using (var conn = new OleDbConnection(connStr))
                    {
                        conn.Open();

                        // Get first user table name
                        DataTable tables = conn.GetSchema("Tables");
                        string tableName = null;
                        foreach (DataRow row in tables.Rows)
                        {
                            string type = row["TABLE_TYPE"].ToString();
                            string name = row["TABLE_NAME"].ToString();
                            if (type.Equals("TABLE", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
                            {
                                tableName = name;
                                break;
                            }
                        }

                        if (string.IsNullOrEmpty(tableName))
                        {
                            MessageBox.Show("No se encontró ninguna tabla de usuario en la base de datos Access.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        string query = $"SELECT * FROM [{tableName}]";
                        using (var cmd = new OleDbCommand(query, conn))
                        using (var adapter = new OleDbDataAdapter(cmd))
                        {
                            var dt = new DataTable();
                            adapter.Fill(dt);
                            dataGridView1.DataSource = dt;
                        }
                    }
                }
                else
                {
                    // Treat as CSV/text file
                    var dt = new DataTable();

                    var allLines = File.ReadAllLines(selectedFilePath, Encoding.Default);
                    if (allLines.Length == 0)
                    {
                        MessageBox.Show("El archivo está vacío.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    // Detect delimiter by checking header line (comma or semicolon)
                    char delimiter = allLines[0].Contains(';') && !allLines[0].Contains(',') ? ';' : ',';

                    var headers = SplitCsvLine(allLines[0], delimiter);
                    foreach (var header in headers)
                    {
                        string colName = string.IsNullOrWhiteSpace(header) ? "Column" + dt.Columns.Count : header;
                        dt.Columns.Add(colName);
                    }

                    for (int i = 1; i < allLines.Length; i++)
                    {
                        if (string.IsNullOrWhiteSpace(allLines[i])) continue;
                        var fields = SplitCsvLine(allLines[i], delimiter);
                        // Ensure table has enough columns
                        while (fields.Length > dt.Columns.Count)
                            dt.Columns.Add();

                        var row = dt.NewRow();
                        for (int c = 0; c < fields.Length; c++)
                            row[c] = fields[c];

                        dt.Rows.Add(row);
                    }

                    dataGridView1.DataSource = dt;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ocurrió un error al leer el archivo:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // CSV parsing helper
        private string[] SplitCsvLine(string line, char delimiter)
        {
            var fields = new List<string>();
            if (line == null) return fields.ToArray();

            var current = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // Check for escaped quote
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++; // skip next
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == delimiter)
                    {
                        fields.Add(current.ToString());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
            }

            fields.Add(current.ToString());
            return fields.ToArray();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}

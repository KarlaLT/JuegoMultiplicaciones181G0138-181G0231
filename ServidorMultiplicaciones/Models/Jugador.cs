using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServidorMultiplicaciones.Models
{
    public class Jugador
    {
        public string Nombre { get; set; }
        public int Respuesta { get; set; }
        public int Tiempo { get; set; }
        public bool Correcto { get; set; }
    }
}

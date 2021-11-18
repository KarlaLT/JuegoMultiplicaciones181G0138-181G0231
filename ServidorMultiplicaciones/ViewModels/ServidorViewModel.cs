using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServidorMultiplicaciones.Views;
using ServidorMultiplicaciones.Models;
using System.Net;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;
using System.Windows;
using System.Threading;

namespace ServidorMultiplicaciones.ViewModels
{
    public class ServidorViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        System.Timers.Timer t = new System.Timers.Timer();
        StartView startView;
        RondaView rondaView;
        TablaResultadosView tablaView;
        static object bloqueo = true;
        HttpListener server = new();
        public List<string> Jugadores { get; set; }
        public bool? RecibirRespuestas { get; set; }
        public ObservableCollection<Jugador> RespuestasUsuarios { get; set; } = new();
        public byte Num1 { get; set; }
        public byte Num2 { get; set; }
        public int RespuestaCorrecta { get; set; }
        public Stopwatch Cronometro { get; set; } = new();
        private Control control;
        public Control Control
        {
            get { return control; }
            set
            {
                control = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Control"));
            }
        }
        public IEnumerable<Jugador> TablaResultados =>
            RespuestasUsuarios.OrderBy(x => x.Correcto == false).ThenBy(x => x.Tiempo);
        private int segundos = 30;
        public int SegundosRestantes
        {
            get { return segundos; }
            set
            {
                segundos = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SegundosRestantes"));
            }
        }
        public ICommand IniciarCommand { get; set; }
        Dispatcher dispatcher;
        public ServidorViewModel()
        {
            startView = new StartView() { DataContext = this };
            rondaView = new RondaView() { DataContext = this };
            tablaView = new TablaResultadosView() { DataContext = this };

            IniciarCommand = new RelayCommand(EjecutarRonda);
            Control = startView;
            RecibirRespuestas = false;
            Jugadores = new();
            dispatcher = Dispatcher.CurrentDispatcher;
            new Thread(Start).Start();
            t.Elapsed += T_Elapsed;
        }

        private void T_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            SegundosRestantes -= 1;
            Actualizar("SegundosRestantes");
            if (SegundosRestantes <= 0)
            {
                dispatcher.Invoke(() =>
                 {
                     RecibirRespuestas = false;
                     t.Stop();
                     Actualizar(nameof(RecibirRespuestas));
                     Actualizar(nameof(TablaResultados));
                     RespuestasUsuarios = new();
                     Control = tablaView;
                 });
            }
        }

        public void Start()
        {
            if (!server.IsListening)
            {
                server.Prefixes.Add("http://*:10470/");
                server.Start();
                Receive();
            }
        }
        void Receive()
        {
            while (server.IsListening)
            {
                var context = server.GetContext();

                if (context.Request.Url.AbsolutePath == "/Jugador" && context.Request.HttpMethod == "POST")
                {
                    if (context.Request.QueryString["username"] != null)
                    {
                        if (Jugadores.Contains(context.Request.QueryString["username"]))
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                            var mensaje = JsonConvert.SerializeObject("Nombre de usuario no disponible, intenta de nuevo.");
                            byte[] buffer = Encoding.UTF8.GetBytes(mensaje);
                            context.Response.OutputStream.Write(buffer, 0, buffer.Length);

                        }
                        else
                        {
                            Jugadores.Add(context.Request.QueryString["username"]);
                            context.Response.StatusCode = 200;
                            var mensaje = JsonConvert.SerializeObject("Nombre de usuario registrado.");
                            byte[] buffer = Encoding.UTF8.GetBytes(mensaje);
                            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        var mensaje = JsonConvert.SerializeObject("El nombre de usuario no puede estar vacío.");
                        byte[] buffer = Encoding.UTF8.GetBytes(mensaje);
                        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    }

                    context.Response.Close();

                }
                else if (context.Request.Url.AbsolutePath == "/Intentar" && context.Request.HttpMethod == "POST")
                {
                    if (RecibirRespuestas == true)
                    {
                        var nombre = context.Request.QueryString["username"];
                        var valor = context.Request.QueryString["valor"];

                        if (valor != null)
                        {
                            if (!RespuestasUsuarios.Any(x => x.Nombre == nombre))
                            {
                                if (Jugadores.Contains(nombre))
                                {

                                    dispatcher.Invoke(() =>
                                    {
                                        lock (bloqueo)
                                        {
                                            RespuestasUsuarios.Add(new Jugador
                                            {
                                                Respuesta = int.Parse(valor),
                                                Nombre = nombre,
                                                Tiempo = Cronometro.Elapsed.Seconds,
                                                Correcto = int.Parse(context.Request.QueryString["valor"]) == RespuestaCorrecta
                                            });
                                        }
                                    });
                                    context.Response.StatusCode = 200;
                                    var mensaje = JsonConvert.SerializeObject("Tu respuesta se recibió correctamente.");
                                    byte[] buffer = Encoding.UTF8.GetBytes(mensaje);
                                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);

                                }
                                else
                                {
                                    var mensaje = JsonConvert.SerializeObject("Tu usuario no está registrado.");
                                    byte[] buffer = Encoding.UTF8.GetBytes(mensaje);
                                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                                    context.Response.StatusCode = 409;
                                }
                            }
                            else
                            {
                                var mensaje = JsonConvert.SerializeObject("Ya has respondido.");
                                byte[] buffer = Encoding.UTF8.GetBytes(mensaje);
                                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                                context.Response.StatusCode = 409;
                            }
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                            var mensaje = JsonConvert.SerializeObject("Ingrese su respuesta.");
                            byte[] buffer = Encoding.UTF8.GetBytes(mensaje);
                            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        }

                    }
                    else
                    {
                        context.Response.StatusCode = 409;
                        var mensaje = JsonConvert.SerializeObject("El servidor no ha iniciado la ronda.");
                        byte[] buffer = Encoding.UTF8.GetBytes(mensaje);
                        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    }

                    context.Response.Close();
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
        }

        private void EjecutarRonda()
        {
            SegundosRestantes = 30;
            Control = rondaView;
            Random r = new();
            Num1 = (byte)r.Next(1, 11);
            Num2 = (byte)r.Next(1, 11);
            RespuestaCorrecta = Num1 * Num2;
            RecibirRespuestas = true;
            Cronometro.Reset();
            Cronometro.Start();
            t.Interval = 1000;
            t.Start();
            Actualizar();
        }

        void Actualizar(string propiedad = null)
        {
            //    dispatcher.Invoke(() =>
            //    {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
            //    });           
        }
    }
}

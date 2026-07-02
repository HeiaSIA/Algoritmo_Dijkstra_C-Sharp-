var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var estado = new EstadoGrafo();

app.MapGet("/", (HttpContext http) =>
{
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
    http.Response.Headers.Pragma = "no-cache";
    http.Response.Headers.Expires = "0";
    return Results.File(Path.Combine(app.Environment.ContentRootPath, "src", "resources", "index.html"), "text/html");
});
app.MapGet("/api", (HttpContext http) =>
{
    var action = http.Request.Query["action"].ToString();
    var response = ProcesarAccion(estado, action, http.Request.Query);
    return Results.Json(response);
});

app.Run();

static object ProcesarAccion(EstadoGrafo estado, string action, IQueryCollection query)
{
    return action switch
    {
        "get" => estado.GetEstado(),
        "click" => Click(estado, query),
        "cambiarModo" => CambiarModo(estado, query),
        "eliminar" => Eliminar(estado),
        "addArista" => AddArista(estado, query),
        "actualizarPeso" => ActualizarPeso(estado, query),
        "editarNodo" => EditarNodo(estado, query),
        "dijkstra" => CalcularDijkstra(estado, query),
        _ => estado.GetEstado()
    };
}

static object Click(EstadoGrafo estado, IQueryCollection query)
{
    if (!double.TryParse(query["x"], out var x) || !double.TryParse(query["y"], out var y))
        return estado.GetEstado();

    if (estado.Modo == "nodo")
    {
        var xVisible = Math.Clamp(x, 20, 730);
        var yVisible = Math.Clamp(y, 20, 500);
        var etiqueta = ObtenerSiguienteEtiqueta(estado.Nodos);
        estado.Nodos.Add(new Nodo(estado.SiguienteId++, xVisible, yVisible, etiqueta));
        estado.Logs.Add($"Nodo {etiqueta} creado en ({(int)xVisible}, {(int)yVisible})");
        estado.NodoSeleccionado = null;
        return estado.GetEstado();
    }

    if (estado.Modo == "editarNodo")
    {
        var nodoEditar = estado.Nodos.FirstOrDefault(n => Math.Abs(n.X - x) <= 25 && Math.Abs(n.Y - y) <= 25);
        if (nodoEditar is not null)
        {
            estado.SolicitarEdicionNodo = new SolicitudEdicionNodo { Id = nodoEditar.Id, Nombre = nodoEditar.Etiqueta };
            estado.SolicitarArista = null;
            estado.SolicitarEdicionArista = null;
        }
        return estado.GetEstado();
    }

    var nodo = estado.Nodos.FirstOrDefault(n => Math.Abs(n.X - x) <= 25 && Math.Abs(n.Y - y) <= 25);
    if (nodo is null)
        return estado.GetEstado();

    if (estado.NodoSeleccionado is null)
    {
        estado.NodoSeleccionado = nodo.Id;
        estado.Logs.Add($"Nodo {nodo.Id} seleccionado");
        return estado.GetEstado();
    }

    if (estado.NodoSeleccionado == nodo.Id)
    {
        estado.NodoSeleccionado = null;
        return estado.GetEstado();
    }

    var desde = estado.NodoSeleccionado.Value;
    var hasta = nodo.Id;
    estado.NodoSeleccionado = null;

    if (estado.Modo == "arista")
    {
        estado.SolicitarArista = new SolicitudArista { Desde = desde, Hasta = hasta };
    }
    else if (estado.Modo == "editar")
    {
        estado.SolicitarEdicionArista = new SolicitudEdicionArista { Desde = desde, Hasta = hasta, Actual = estado.BuscarArista(desde, hasta)?.Weight ?? 0 };
    }

    return estado.GetEstado();
}

static object CambiarModo(EstadoGrafo estado, IQueryCollection query)
{
    estado.Modo = query["modo"].ToString();
    estado.NodoSeleccionado = null;
    estado.SolicitarArista = null;
    estado.SolicitarEdicionArista = null;
    estado.SolicitarEdicionNodo = null;
    estado.Logs.Add($"Modo cambiado a {estado.Modo}");
    return estado.GetEstado();
}

static object Eliminar(EstadoGrafo estado)
{
    if (estado.Nodos.Count == 0)
        return estado.GetEstado();

    var ultimo = estado.Nodos.OrderBy(n => n.Id).Last();
    estado.Nodos.Remove(ultimo);
    estado.Aristas.RemoveAll(a => a.Desde == ultimo.Id || a.Hasta == ultimo.Id);
    estado.Logs.Add($"Nodo {ultimo.Etiqueta} eliminado");
    return estado.GetEstado();
}

static object AddArista(EstadoGrafo estado, IQueryCollection query)
{
    if (!int.TryParse(query["desde"], out var desde) || !int.TryParse(query["hasta"], out var hasta) || !int.TryParse(query["peso"], out var peso))
        return estado.GetEstado();

    if (estado.BuscarArista(desde, hasta) is not null)
    {
        estado.Logs.Add("La arista ya existe");
        return estado.GetEstado();
    }

    estado.Aristas.Add(new Arista(desde, hasta, peso));
    estado.Logs.Add($"Arista creada entre {desde} y {hasta} con peso {peso}");
    estado.SolicitarArista = null;
    return estado.GetEstado();
}

static object ActualizarPeso(EstadoGrafo estado, IQueryCollection query)
{
    if (!int.TryParse(query["desde"], out var desde) || !int.TryParse(query["hasta"], out var hasta) || !int.TryParse(query["peso"], out var peso))
        return estado.GetEstado();

    var arista = estado.BuscarArista(desde, hasta);
    if (arista is null)
        return estado.GetEstado();

    arista.Weight = peso;
    estado.Logs.Add($"Peso actualizado entre {desde} y {hasta}: {peso}");
    estado.SolicitarEdicionArista = null;
    return estado.GetEstado();
}

static object EditarNodo(EstadoGrafo estado, IQueryCollection query)
{
    if (!int.TryParse(query["id"], out var id))
        return estado.GetEstado();

    var nodo = estado.Nodos.FirstOrDefault(n => n.Id == id);
    if (nodo is null)
        return estado.GetEstado();

    var nombre = query["nombre"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(nombre))
    {
        estado.Logs.Add("El nombre del nodo no puede estar vacío");
        estado.SolicitarEdicionNodo = null;
        return estado.GetEstado();
    }

    nodo.Etiqueta = nombre;
    estado.Logs.Add($"Nodo {nombre} actualizado");
    estado.SolicitarEdicionNodo = null;
    return estado.GetEstado();
}

static string ObtenerSiguienteEtiqueta(List<Nodo> nodos)
{
    var usados = nodos.Select(n => n.Etiqueta?.Trim().ToUpperInvariant())
        .Where(e => !string.IsNullOrWhiteSpace(e))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    for (var i = 0; i < 26; i++)
    {
        var letra = ((char)('A' + i)).ToString();
        if (!usados.Contains(letra))
            return letra;
    }

    return $"N{nodos.Count + 1}";
}

static object CalcularDijkstra(EstadoGrafo estado, IQueryCollection query)
{
    if (!int.TryParse(query["origen"], out var origen) || !int.TryParse(query["destino"], out var destino))
        return estado.GetEstado();

    var nodos = estado.Nodos.Select(n => n.Id).ToHashSet();
    if (!nodos.Contains(origen) || !nodos.Contains(destino))
    {
        estado.Logs.Add("Debes seleccionar nodos válidos");
        return estado.GetEstado();
    }

    var distancias = estado.Nodos.ToDictionary(n => n.Id, _ => int.MaxValue);
    var previos = new Dictionary<int, int?>();
    var visitados = new HashSet<int>();
    distancias[origen] = 0;

    while (true)
    {
        var actual = distancias.Where(d => !visitados.Contains(d.Key)).OrderBy(d => d.Value).FirstOrDefault();
        if (actual.Value == int.MaxValue)
            break;

        visitados.Add(actual.Key);
        if (actual.Key == destino)
            break;

        foreach (var arista in estado.Aristas.Where(a => a.Desde == actual.Key || a.Hasta == actual.Key))
        {
            var vecino = arista.Desde == actual.Key ? arista.Hasta : arista.Desde;
            if (visitados.Contains(vecino))
                continue;

            var nuevoCosto = actual.Value + arista.Weight;
            if (nuevoCosto < distancias[vecino])
            {
                distancias[vecino] = nuevoCosto;
                previos[vecino] = actual.Key;
            }
        }
    }

    var ruta = new List<int>();
    var cursor = destino;
    while (previos.ContainsKey(cursor))
    {
        ruta.Add(cursor);
        cursor = previos[cursor] ?? cursor;
        if (cursor == origen)
            break;
    }
    ruta.Add(origen);
    ruta.Reverse();

    estado.RutaResaltada = ruta.Zip(ruta.Skip(1), (a, b) => new RutaResaltada { De = a, A = b }).ToList();
    estado.Logs.Add(distancias[destino] == int.MaxValue
        ? $"No existe ruta entre {origen} y {destino}"
        : $"Ruta mínima desde {origen} hasta {destino}: {string.Join(" -> ", ruta)}");

    return estado.GetEstado();
}

public class EstadoGrafo
{
    public string Modo { get; set; } = "nodo";
    public List<Nodo> Nodos { get; set; } = [];
    public List<Arista> Aristas { get; set; } = [];
    public List<string> Logs { get; set; } = [];
    public List<RutaResaltada> RutaResaltada { get; set; } = [];
    public int? NodoSeleccionado { get; set; }
    public SolicitudArista? SolicitarArista { get; set; }
    public SolicitudEdicionArista? SolicitarEdicionArista { get; set; }
    public SolicitudEdicionNodo? SolicitarEdicionNodo { get; set; }
    public int SiguienteId { get; set; } = 0;

    public Arista? BuscarArista(int desde, int hasta) => Aristas.FirstOrDefault(a => (a.Desde == desde && a.Hasta == hasta) || (a.Desde == hasta && a.Hasta == desde));

    public object GetEstado() => new
    {
        modo = Modo,
        nodos = Nodos,
        aristas = Aristas,
        logs = Logs,
        rutaResaltada = RutaResaltada,
        nodoSeleccionado = NodoSeleccionado,
        solicitarArista = SolicitarArista,
        solicitarEdicionArista = SolicitarEdicionArista,
        solicitarEdicionNodo = SolicitarEdicionNodo,
        alert = (string?)null
    };
}

public class Nodo
{
    public Nodo(int id, double x, double y, string? etiqueta = null)
    {
        Id = id;
        X = x;
        Y = y;
        Etiqueta = string.IsNullOrWhiteSpace(etiqueta) ? GenerarEtiquetaDesdeId(id) : etiqueta;
    }

    public int Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public string Etiqueta { get; set; }

    private static string GenerarEtiquetaDesdeId(int id)
    {
        return id >= 0 && id < 26 ? ((char)('A' + id)).ToString() : $"N{id}";
    }
}

public class Arista
{
    public Arista(int desde, int hasta, int weight)
    {
        Desde = desde;
        Hasta = hasta;
        Weight = weight;
    }

    public int Desde { get; set; }
    public int Hasta { get; set; }
    public int Weight { get; set; }
}

public class RutaResaltada
{
    public int De { get; set; }
    public int A { get; set; }
}

public class SolicitudArista
{
    public int Desde { get; set; }
    public int Hasta { get; set; }
}

public class SolicitudEdicionArista
{
    public int Desde { get; set; }
    public int Hasta { get; set; }
    public int Actual { get; set; }
}

public class SolicitudEdicionNodo
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
}

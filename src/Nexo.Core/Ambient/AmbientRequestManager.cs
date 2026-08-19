namespace Nexo.Core.Ambient;

public sealed class AmbientRequestManager
{
    private static readonly TimeSpan HistoryRetention = TimeSpan.FromDays(90);
    private const int HistoryCap = 500;

    private readonly object _sync = new();
    private readonly IAmbientRequestHistoryStore _store;
    private AmbientRequestState _state = new();

    public AmbientRequestManager(IAmbientRequestHistoryStore store)
    {
        _store = store;
    }

    public void Load()
    {
        lock (_sync)
        {
            _state = Normalize(_store.Load());
            CloseRequestLeftRunningByAPreviousProcessLocked(DateTimeOffset.Now);
        }
    }

    /// <summary>
    /// Diseño D63 — una solicitud en curso no sobrevive al proceso que la estaba atendiendo.
    ///
    /// Si Kohana se cierra —o la matan— mientras hay una solicitud escuchando, pensando o
    /// respondiendo, ese estado queda escrito en disco y al arrancar de nuevo nadie lo va a
    /// terminar: quien iba a hacerlo ya no existe. Y como <see cref="Begin"/> rechaza una solicitud
    /// nueva mientras haya otra en curso, **la función entera queda bloqueada para siempre**.
    ///
    /// No es hipotético. En el equipo de Adler el archivo tenía una solicitud en estado Escuchando
    /// del 2 de agosto: dieciséis días en los que cada orden de Lens respondía "ya hay una
    /// solicitud ambiental en curso" y nadie sabía por qué.
    ///
    /// Se archiva en vez de dejarse visible: es de otra sesión, y abrir Kohana con el error de algo
    /// que pasó hace días sería ruido, no información. El historial sí lo conserva.
    /// </summary>
    private void CloseRequestLeftRunningByAPreviousProcessLocked(DateTimeOffset now)
    {
        if (_state.ActiveRequest is not { } request ||
            request.Status is not (AmbientRequestStatus.Listening
                or AmbientRequestStatus.Thinking
                or AmbientRequestStatus.Streaming))
        {
            return;
        }

        request.Status = AmbientRequestStatus.Failed;
        request.ErrorMessage = "Kohana se cerró mientras la solicitud estaba en curso.";
        request.UpdatedAt = now;
        ArchiveActiveLocked(now);
        SaveLocked();
    }

    public AmbientRequestSnapshot GetSnapshot(int recentCount = 5)
    {
        lock (_sync)
        {
            var recent = _state.History
                .OrderByDescending(entry => entry.CompletedAt)
                .Take(Math.Max(0, recentCount))
                .Select(entry => entry.Copy())
                .ToList();

            return new AmbientRequestSnapshot(_state.ActiveRequest?.Copy(), recent);
        }
    }

    /// <summary>
    /// Diseño D4 — inicia una nueva solicitud (estado Escuchando). Si la solicitud activa anterior
    /// ya llegó a un estado terminal visible (Resultado/Cancelada/Error), se archiva en el
    /// historial automáticamente: el usuario no tiene que descartar manualmente un resultado ya
    /// leído antes de preguntar algo nuevo. Si sigue en curso (Escuchando/Pensando), se rechaza —
    /// primero hay que cancelarla.
    /// </summary>
    public AmbientRequestOperationResult Begin(
        string prompt,
        AmbientContextSnapshot? context,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return AmbientRequestOperationResult.Failed(
                "La solicitud necesita un contenido para escuchar.");
        }

        lock (_sync)
        {
            if (_state.ActiveRequest is
                { Status: AmbientRequestStatus.Listening or AmbientRequestStatus.Thinking
                    or AmbientRequestStatus.Streaming })
            {
                return AmbientRequestOperationResult.Failed(
                    "Ya hay una solicitud en curso. Cancélala antes de iniciar otra.");
            }

            if (_state.ActiveRequest is not null)
            {
                ArchiveActiveLocked(now);
            }

            var request = new AmbientRequest
            {
                Id = Guid.NewGuid(),
                Prompt = prompt.Trim(),
                Status = AmbientRequestStatus.Listening,
                CreatedAt = now,
                UpdatedAt = now,
                Context = context
            };

            _state.ActiveRequest = request;
            SaveLocked();
            return AmbientRequestOperationResult.Completed(
                "Escuchando la solicitud.", request.Copy());
        }
    }

    public AmbientRequestOperationResult BeginThinking(DateTimeOffset now)
    {
        lock (_sync)
        {
            var request = _state.ActiveRequest;
            if (request is null || request.Status != AmbientRequestStatus.Listening)
            {
                return AmbientRequestOperationResult.Failed(
                    "No hay una solicitud escuchando para procesar.");
            }

            request.Status = AmbientRequestStatus.Thinking;
            request.UpdatedAt = now;
            SaveLocked();
            return AmbientRequestOperationResult.Completed("Pensando.", request.Copy());
        }
    }

    /// <summary>
    /// Diseño D7 — pasa a mostrar la respuesta conforme llega, en vez de esperar a tenerla
    /// completa. No guarda en disco en cada fragmento a propósito: una respuesta larga dispararía
    /// cientos de escrituras del archivo de historial por una sola solicitud. El estado se
    /// persiste al completar (o al fallar), que es cuando hay algo definitivo que recordar.
    /// </summary>
    public AmbientRequestOperationResult BeginStreaming(DateTimeOffset now)
    {
        lock (_sync)
        {
            var request = _state.ActiveRequest;
            if (request is null ||
                request.Status is not (AmbientRequestStatus.Listening or AmbientRequestStatus.Thinking))
            {
                return AmbientRequestOperationResult.Failed(
                    "No hay una solicitud en curso que pueda empezar a responder.");
            }

            request.Status = AmbientRequestStatus.Streaming;
            request.PartialText = string.Empty;
            request.UpdatedAt = now;
            SaveLocked();
            return AmbientRequestOperationResult.Completed("Respondiendo.", request.Copy());
        }
    }

    public AmbientRequestOperationResult AppendStreamedText(string? chunk, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(chunk))
        {
            return AmbientRequestOperationResult.Failed("No hay texto que añadir.");
        }

        lock (_sync)
        {
            var request = _state.ActiveRequest;
            if (request is null || request.Status != AmbientRequestStatus.Streaming)
            {
                return AmbientRequestOperationResult.Failed(
                    "No hay una respuesta en curso a la que añadir texto.");
            }

            request.PartialText += chunk;
            request.UpdatedAt = now;
            return AmbientRequestOperationResult.Completed(request.PartialText, request.Copy());
        }
    }

    /// <summary>
    /// Diseño D7 — cierra una respuesta que llegó por partes usando el texto ya acumulado, para que
    /// quien orquesta no tenga que volver a juntarlo por su cuenta.
    /// </summary>
    public AmbientRequestOperationResult CompleteStreamedResult(
        IReadOnlyList<AmbientQuickAction> quickActions,
        bool canUndo,
        DateTimeOffset now,
        Func<string, string>? summarize = null)
    {
        lock (_sync)
        {
            var request = _state.ActiveRequest;
            if (request is null || request.Status != AmbientRequestStatus.Streaming)
            {
                return AmbientRequestOperationResult.Failed(
                    "No hay una respuesta en curso que completar.");
            }

            var fullText = request.PartialText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fullText))
            {
                request.Status = AmbientRequestStatus.Failed;
                request.ErrorMessage = "La respuesta llegó vacía.";
                request.UpdatedAt = now;
                SaveLocked();
                return AmbientRequestOperationResult.Failed("La respuesta llegó vacía.");
            }

            request.Status = AmbientRequestStatus.Result;
            request.Result = new AmbientRequestResult(
                summarize is null ? fullText : summarize(fullText),
                fullText,
                quickActions,
                canUndo);
            request.UpdatedAt = now;
            SaveLocked();
            return AmbientRequestOperationResult.Completed(fullText, request.Copy());
        }
    }

    public AmbientRequestOperationResult CompleteWithResult(
        AmbientRequestResult result,
        DateTimeOffset now)
    {
        lock (_sync)
        {
            var request = _state.ActiveRequest;
            if (request is null ||
                request.Status is not (AmbientRequestStatus.Listening or AmbientRequestStatus.Thinking
                    or AmbientRequestStatus.Streaming))
            {
                return AmbientRequestOperationResult.Failed(
                    "No hay una solicitud en curso para completar.");
            }

            request.Status = AmbientRequestStatus.Result;
            request.Result = result;
            request.UpdatedAt = now;
            SaveLocked();
            return AmbientRequestOperationResult.Completed(result.ShortText, request.Copy());
        }
    }

    public AmbientRequestOperationResult Fail(string errorMessage, DateTimeOffset now)
    {
        var message = string.IsNullOrWhiteSpace(errorMessage)
            ? "Ocurrió un error al procesar la solicitud."
            : errorMessage.Trim();

        lock (_sync)
        {
            var request = _state.ActiveRequest;
            if (request is null ||
                request.Status is not (AmbientRequestStatus.Listening or AmbientRequestStatus.Thinking
                    or AmbientRequestStatus.Streaming))
            {
                return AmbientRequestOperationResult.Failed(
                    "No hay una solicitud en curso para marcar como fallida.");
            }

            request.Status = AmbientRequestStatus.Failed;
            request.ErrorMessage = message;
            request.UpdatedAt = now;
            SaveLocked();
            return AmbientRequestOperationResult.Completed(message, request.Copy());
        }
    }

    public AmbientRequestOperationResult Cancel(DateTimeOffset now)
    {
        lock (_sync)
        {
            var request = _state.ActiveRequest;
            if (request is null)
            {
                return AmbientRequestOperationResult.Failed("No hay una solicitud activa.");
            }

            request.Status = AmbientRequestStatus.Cancelled;
            request.UpdatedAt = now;
            ArchiveActiveLocked(now);
            SaveLocked();
            return AmbientRequestOperationResult.Completed("Cancelé la solicitud.");
        }
    }

    /// <summary>
    /// Diseño D4 — descarta manualmente la solicitud visible (Resultado/Cancelada/Error) sin
    /// esperar a que una nueva solicitud la archive. Rechaza si sigue en curso: descartar algo que
    /// todavía está pensando equivale a cancelarlo, y eso debe pedirse explícitamente con
    /// <see cref="Cancel"/> para que quede registrado como cancelación, no como "terminó solo".
    /// </summary>
    public AmbientRequestOperationResult Dismiss(DateTimeOffset now)
    {
        lock (_sync)
        {
            var request = _state.ActiveRequest;
            if (request is null)
            {
                return AmbientRequestOperationResult.Failed(
                    "No hay ninguna solicitud para descartar.");
            }

            if (request.Status is AmbientRequestStatus.Listening or AmbientRequestStatus.Thinking
                or AmbientRequestStatus.Streaming)
            {
                return AmbientRequestOperationResult.Failed(
                    "La solicitud sigue en curso. Cancélala en vez de descartarla.");
            }

            ArchiveActiveLocked(now);
            SaveLocked();
            return AmbientRequestOperationResult.Completed("Descarté la solicitud.");
        }
    }

    /// <summary>
    /// Diseño D4 — primitiva de deshacer: solo marca la solicitud correspondiente como deshecha si
    /// su resultado declaró <c>CanUndo</c>. D4 no ejecuta ninguna reversión real todavía (no hay
    /// acciones con efecto real que deshacer en esta fase) — esto es la primitiva de estado que las
    /// fases 4/5/7 usarán para saber qué ya se marcó como revertido.
    /// </summary>
    public AmbientRequestOperationResult Undo(Guid requestId, DateTimeOffset now)
    {
        lock (_sync)
        {
            if (_state.ActiveRequest is { } active && active.Id == requestId)
            {
                if (active.Result is not { CanUndo: true })
                {
                    return AmbientRequestOperationResult.Failed(
                        "Esta solicitud no tiene una acción que deshacer.");
                }

                if (active.Undone)
                {
                    return AmbientRequestOperationResult.Failed("Ya se deshizo esta solicitud.");
                }

                active.Undone = true;
                active.UpdatedAt = now;
                SaveLocked();
                return AmbientRequestOperationResult.Completed(
                    "Deshice la solicitud.", active.Copy());
            }

            var entry = _state.History.FirstOrDefault(item => item.Id == requestId);
            if (entry is null)
            {
                return AmbientRequestOperationResult.Failed(
                    "No encontré esa solicitud en el historial.");
            }

            if (!entry.CanUndo)
            {
                return AmbientRequestOperationResult.Failed(
                    "Esta solicitud no tiene una acción que deshacer.");
            }

            if (entry.Undone)
            {
                return AmbientRequestOperationResult.Failed("Ya se deshizo esta solicitud.");
            }

            entry.Undone = true;
            SaveLocked();
            return AmbientRequestOperationResult.Completed("Deshice la solicitud.");
        }
    }

    /// <summary>
    /// Diseño D63 — cierra una solicitud que se quedó esperando una respuesta que no va a llegar.
    ///
    /// Existe porque el camino de excepciones no basta. El cliente de IA corta la petición a los
    /// noventa segundos, y cuando ese corte ocurre **a mitad del flujo** llega como
    /// <c>OperationCanceledException</c> desde dentro de un iterador, que en C# no puede envolver
    /// sus <c>yield</c> en un <c>catch</c>. Bastó con que un filtro de excepción dejara pasar ese
    /// caso para que la solicitud se quedara en "Pensando…" para siempre: eso es justo lo que Adler
    /// reportó, y el historial guarda varias entradas ancladas en <c>Streaming</c> que lo
    /// atestiguan.
    ///
    /// Arreglar el filtro es necesario y no es suficiente: cualquier otro camino que se olvide de
    /// cerrar la solicitud vuelve a dejar la píldora colgada. Esto lo comprueba el reloj, que no
    /// depende de que nadie se acuerde.
    ///
    /// Lo que ya llegó no se tira. Con texto parcial la solicitud se cierra COMO RESULTADO —media
    /// respuesta es más que ninguna, y es la misma regla que D7 fijó para el fallo a mitad—; sin
    /// texto se cierra como fallo.
    /// </summary>
    /// <remarks>
    /// Los dos plazos son distintos a propósito. Antes del primer fragmento hay que darle margen a
    /// un modelo local sobre una gráfica modesta, que puede tardar minutos en arrancar. Una vez que
    /// los fragmentos empezaron a llegar, dos minutos de silencio ya no son lentitud: es una
    /// conexión muerta.
    /// </remarks>
    /// <summary>
    /// Escuchando es el estado entre <see cref="Begin"/> y el primer paso asíncrono, o sea
    /// milisegundos. Un minuto ahí no es lentitud: es que quien la abrió se fue sin cerrarla.
    /// </summary>
    public static readonly TimeSpan ListeningStallLimit = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan ThinkingStallLimit = TimeSpan.FromMinutes(3);

    public static readonly TimeSpan StreamingSilenceLimit = TimeSpan.FromMinutes(2);

    public AmbientRequestOperationResult FailIfStalled(DateTimeOffset now)
    {
        lock (_sync)
        {
            var request = _state.ActiveRequest;
            if (request is null)
            {
                return AmbientRequestOperationResult.Failed("No hay ninguna solicitud activa.");
            }

            var limit = request.Status switch
            {
                AmbientRequestStatus.Listening => ListeningStallLimit,
                AmbientRequestStatus.Thinking => ThinkingStallLimit,
                AmbientRequestStatus.Streaming => StreamingSilenceLimit,
                _ => (TimeSpan?)null
            };

            if (limit is not { } stallLimit || now - request.UpdatedAt < stallLimit)
            {
                return AmbientRequestOperationResult.Failed("La solicitud sigue viva.");
            }

            var partial = request.PartialText;
            if (!string.IsNullOrWhiteSpace(partial))
            {
                request.Status = AmbientRequestStatus.Result;
                request.Result = new AmbientRequestResult(
                    partial,
                    partial,
                    [],
                    CanUndo: false);
                request.ErrorMessage = "La respuesta se cortó y Kohana dejó de esperarla.";
                request.UpdatedAt = now;
                SaveLocked();
                return AmbientRequestOperationResult.Completed(
                    "La respuesta se cortó; queda lo que alcanzó a llegar.",
                    request.Copy());
            }

            request.Status = AmbientRequestStatus.Failed;
            request.ErrorMessage = "La respuesta tardó demasiado y Kohana dejó de esperarla.";
            request.UpdatedAt = now;
            SaveLocked();
            return AmbientRequestOperationResult.Completed(
                request.ErrorMessage,
                request.Copy());
        }
    }

    public IReadOnlyList<AmbientRequestHistoryEntry> GetHistory()
    {
        lock (_sync)
        {
            return _state.History.Select(entry => entry.Copy()).ToList();
        }
    }

    private void ArchiveActiveLocked(DateTimeOffset now)
    {
        var request = _state.ActiveRequest;
        if (request is null)
        {
            return;
        }

        var entry = new AmbientRequestHistoryEntry
        {
            Id = request.Id,
            Prompt = request.Prompt,
            Status = request.Status,
            CreatedAt = request.CreatedAt,
            CompletedAt = now,
            ResultShortText = request.Result?.ShortText,
            ErrorMessage = request.ErrorMessage,
            CanUndo = request.Result?.CanUndo ?? false,
            Undone = request.Undone
        };

        _state.History.Add(entry);
        _state.ActiveRequest = null;
        TrimHistoryLocked(now);
    }

    private void SaveLocked() => _store.Save(_state.Copy());

    private void TrimHistoryLocked(DateTimeOffset now)
    {
        var cutoff = now.Subtract(HistoryRetention);
        _state.History = _state.History
            .Where(entry => entry.CompletedAt >= cutoff)
            .OrderBy(entry => entry.CompletedAt)
            .TakeLast(HistoryCap)
            .ToList();
    }

    private static AmbientRequestState Normalize(AmbientRequestState? state)
    {
        state ??= new AmbientRequestState();
        state.History ??= [];
        state.History = state.History
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Prompt))
            .Select(entry => entry.Copy())
            .ToList();

        if (state.ActiveRequest is { } request && string.IsNullOrWhiteSpace(request.Prompt))
        {
            state.ActiveRequest = null;
        }

        return state;
    }
}

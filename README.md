# FleetProof Web Services

Backend real de FleetProof para AV2: ASP.NET Core, EF Core y PostgreSQL. Esta
versión implementa flujos locales en los seis contextos del mapa de FleetProof.
Los pagos y las fuentes del reporte son simulados; no consulta sistemas externos
ni ejecuta trabajos periódicos en segundo plano.
No tiene cuentas de prueba preinstaladas ni copia datos del mock.

## Contextos

- `UserManagement/`: registro, inicio de sesión, perfil y eliminación de la cuenta. Un cambio de contraseña invalida sesiones anteriores.
- `VehicleInformation/`: registro, consulta y edición de vehículos propios por placa. Es la fuente interna de datos para flotas, reportes de flota y monitoreo.
- `SubscriptionManagement/`: planes semanales, mensuales y anuales, con segmento particular o empresa y confirmación de pago simulada. Fleet Management y Vehicle Monitoring verifican aquí el acceso antes de escribir.
- `FleetManagement/`: creación de flotas y asignación de un vehículo propio a una sola flota, con responsable. También gestiona los casos de resolución que Vue ubica en este contexto.
- `ReportManagement/`: reportes individuales por placa o vinculados a un vehículo propio, con fuentes simuladas y estados de revisión compatibles con Vue. El PDF se genera en Vue.
- `VehicleMonitoring/`: configuración del seguimiento, instantáneas y alertas de cambios de vehículos propios. La comprobación la inicia Vue; el servidor calcula el cambio a partir de los datos persistidos.
- `Shared/`: infraestructura común y contexto de persistencia.

### ACL entre Flotas y Suscripciones

Flotas consume su propio puerto `IFleetEntitlements`. El adaptador
`SubscriptionEntitlementsAdapter` traduce la respuesta del contrato de
Suscripciones a un permiso de gestión. Suscripciones conserva la evaluación
del plan y del estado de pago. El adaptador funciona dentro del proceso, sin
HTTP interno ni acceso directo a la tabla de suscripciones desde Flotas.
Sin permiso, los POST existentes de flotas y asignaciones devuelven 403;
las consultas siguen disponibles. Monitoreo no cambia en este incremento.

Vue genera un QR de Taypi de demostración al abrir el diálogo del plan.
El QR contiene datos no pagables, no llama a Taypi y no solicita datos
bancarios. Cerrar el diálogo no activa el plan; confirmar registra la
activación simulada mediante las rutas existentes de Suscripciones.

La aplicación local conserva el mapa de seis contextos sin convertirlos en
microservicios. PostgreSQL mantiene las tablas en minúsculas; los recursos
estructurados de reportes, casos y alertas se guardan en JSONB con propietario.
El registro de pago de demostración no acredita un cobro real. No incluye
Taypi, consulta a fuentes oficiales, reintentos automáticos ni correo de alertas.

## Ejecución local

Requiere SDK .NET 10, PostgreSQL y, para las pruebas, Node.js 22 o posterior.
Con Docker activo, ejecute los comandos desde la raíz del repositorio. Use una
contraseña local generada o defina los datos de una instancia PostgreSQL existente.

```sh
export POSTGRES_PASSWORD="$(openssl rand -hex 24)"
docker compose up -d --wait
export ConnectionStrings__FleetProof="Host=127.0.0.1;Port=5432;Database=fleetproof;Username=fleetproof;Password=$POSTGRES_PASSWORD"
dotnet tool restore
dotnet ef database update
dotnet run --urls http://127.0.0.1:5080
```

La contraseña de PostgreSQL se configura al crear el volumen; en arranques
posteriores reutilice el mismo valor. No elimine un volumen para cambiar credenciales
si contiene datos que necesita conservar.

## Contrato y pruebas

- Base: `http://127.0.0.1:5080/api/v1`.
- OpenAPI: `http://127.0.0.1:5080/openapi/v1.json`.
- Público: `GET /health`, `POST /authentication/sign-up` y `POST /authentication/sign-in`.
- Con Bearer: `GET/PATCH/DELETE /account`; `GET/POST /vehicles` y
  `GET/PUT /vehicles/{id}`; `GET/POST /fleets`, `GET/POST /assignments`,
  `GET/POST /subscriptions`, `GET/POST /reports`, `GET/POST /cases`,
  `GET/POST /monitorings` y `GET/POST /alerts`. Suscripciones, reportes,
  casos y monitoreos también admiten `PUT /{recurso}/{id}`.
  Los recursos de flotas y asignaciones conservan los
  campos esperados por Vue: `{id,name}` y `{id,fleetId,vehicleId,responsible}`.

Ejecute `node scripts/smoke.mjs` contra una base local desechable. La prueba crea
dos cuentas y verifica activación de plan, aislamiento, asignaciones,
reportes, resolución, alertas y revocación de sesión. Los datos de prueba que
no se eliminan como parte del flujo permanecen en la base.

Para probar Vue localmente, inicie Vite con `VITE_AUTH_MODE=real` y
`VITE_FLEETPROOF_API_URL=http://127.0.0.1:5080/api/v1`. Si usa Vite en
el puerto 5179, configure `Cors__Origins__0=http://127.0.0.1:5179` en la API.
Con Playwright disponible, ejecute `node scripts/vue-smoke.mjs`. La prueba
registra una cuenta, confirma el QR simulado, crea un vehículo y una flota,
realiza la asignación y verifica que siga visible tras recargar. Puede indicar
`PLAYWRIGHT_MODULE` y `CHROME_PATH` si usa una instalación externa.

## Despliegue

Configure la conexión y los orígenes permitidos mediante secretos del entorno
(`ConnectionStrings__FleetProof`, `Cors__Origins__0`). Use HTTPS y aplique
migraciones por separado antes de publicar. El token Bearer expira en una hora;
para conservar sesiones entre instancias y reinicios en producción, configure un
almacén persistente y compartido de claves de ASP.NET Core Data Protection.
No registre contraseñas ni tokens en evidencias o logs.

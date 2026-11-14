// Generates fixtures for the local demo account; pipe to the local PostgreSQL container.
import { createHash } from 'node:crypto';

const catalog = [
  ['QAT-184', 'Toyota', 'Hilux', 2023], ['RMB-257', 'Hyundai', 'Tucson', 2022],
  ['VNX-603', 'Kia', 'Sportage', 2021], ['ZLT-841', 'Nissan', 'Frontier', 2024],
  ['PMK-492', 'Mazda', 'CX-5', 2020], ['HVG-156', 'Ford', 'Ranger', 2023],
  ['KDW-735', 'Chevrolet', 'Tracker', 2024], ['XRS-208', 'Suzuki', 'Swift', 2019],
  ['JQY-521', 'Mitsubishi', 'L200', 2022], ['BVT-963', 'Volkswagen', 'T-Cross', 2021],
  ['NWC-306', 'Renault', 'Duster', 2023], ['LMX-782', 'Subaru', 'Forester', 2020],
  ['FZH-419', 'Honda', 'CR-V', 2021], ['CYD-547', 'Geely', 'Coolray', 2024],
  ['WPK-618', 'Changan', 'CS35 Plus', 2023], ['DSJ-274', 'Great Wall', 'Poer', 2022],
  ['UFB-850', 'Isuzu', 'D-Max', 2020], ['MVQ-139', 'JAC', 'T8 Pro', 2023],
  ['SGK-467', 'Peugeot', '3008', 2021], ['TXY-925', 'Citroën', 'C4 Cactus', 2019],
  ['ALW-372', 'Fiat', 'Strada', 2022], ['EPN-687', 'Audi', 'Q3', 2020],
  ['GVR-236', 'BMW', 'X1', 2021], ['YKC-710', 'Mercedes-Benz', 'GLA 200', 2022],
  ['PJF-582', 'Volvo', 'XC40', 2023], ['QLZ-948', 'MG', 'ZS', 2024],
  ['RXB-365', 'Chery', 'Tiggo 7 Pro', 2022], ['VSD-801', 'Toyota', 'Corolla Cross', 2024],
  ['KNM-624', 'Hyundai', 'Accent', 2018], ['BLQ-173', 'Kia', 'Soluto', 2020],
];
const registry = [
  ['Sin observaciones registrales', 'La ficha no presenta alertas.'],
  ['Cambio de titular registrado', 'El historial muestra una transferencia de titularidad.'],
  ['Actualización de características', 'La ficha registra una actualización de color o categoría.'],
  ['Anotación registral', 'La anotación requiere revisión documental.'],
];
const insurance = [
  ['SOAT vigente', 'La póliza figura activa a la fecha del reporte.'],
  ['SOAT próximo a vencer', 'La póliza vence en menos de 15 días.'],
  ['SOAT vencido', 'La vigencia ya terminó; requiere renovación.'],
];
const accidents = [
  ['Sin accidentes registrados', 'No se muestran siniestros en el historial.'],
  ['Un siniestro leve registrado', 'El historial muestra un incidente menor sin lesiones.'],
  ['Historial con dos incidencias', 'El historial registra dos incidencias en fechas distintas.'],
  ['Registro histórico disponible', 'El historial muestra un siniestro cerrado.'],
  ['Sin accidentes registrados', 'No se encontraron incidencias para la placa.'],
];
const now = Date.now();
const reports = catalog.map(([plate, make, model, year], index) => {
  const id = createHash('md5').update('fleetproof:local-demo:report:' + plate).digest('hex')
    .replace(/^(........)(....)(....)(....)(............)$/, '$1-$2-$3-$4-$5');
  const requestedAt = new Date(now - index * 86400000).toISOString();
  const dateChecked = requestedAt.slice(0, 10);
  const finding = (source, [result, summary], evidence) =>
    ({ source, result, summary, evidence, simulated:true, dateChecked });
  const [result, summary] = registry[index % registry.length];
  return { id, plate, make, model, requestedAt, vehicleId:null, status:'published',
    paymentStatus:'completed', serviceType:'individual',
    findings:[
      finding('Consulta Vehicular', [result, `${make} ${model}, año ${year}, placa ${plate}. ${summary}`], 'Ficha registral'),
      finding('SOAT', insurance[Math.floor(index / 2) % insurance.length], 'Póliza SOAT'),
      finding('SBS Accidentes', accidents[index % accidents.length], 'Historial de siniestros'),
    ] };
});

process.stdout.write(`
BEGIN;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM users WHERE username = 'demo_fleetproof') THEN
        RAISE EXCEPTION 'Local demo account not found';
    END IF;
END $$;
INSERT INTO vehicle_reports (id, user_id, data)
SELECT (fixture->>'id')::uuid, users.id, fixture
FROM users CROSS JOIN jsonb_array_elements($fixtures$${JSON.stringify(reports)}$fixtures$::jsonb) AS fixture
WHERE users.username = 'demo_fleetproof'
ON CONFLICT (id) DO UPDATE SET data = EXCLUDED.data
WHERE vehicle_reports.user_id = EXCLUDED.user_id;
COMMIT;
`);

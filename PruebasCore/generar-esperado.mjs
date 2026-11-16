// Genera esperado.json con los resultados del mapa web, para comprobar que el código C# da lo mismo.
// Uso: node generar-esperado.mjs "/ruta/al/mapa-web/dist"
import {readFileSync,writeFileSync} from 'node:fs';
import {pathToFileURL} from 'node:url';
import {resolve} from 'node:path';
const dist=resolve(process.argv[2]||'.');
const load=file=>import(pathToFileURL(resolve(dist,file)).href);
const {shortestRoute}=await load('js/path-graph.js');
const {distanceMetres,isWithinBoundary}=await load('js/geo-coordinates.js');
const G=await load('js/ar/ar-geo.js');
const data=file=>JSON.parse(readFileSync(resolve(dist,'data',file),'utf8'));
const graph=data('grafo-caminos-esan.json'),boundary=data('esan-boundary.geojson').features[0].geometry.coordinates;
let seed=12345;
const random=()=>(seed=(seed*1664525+1013904223)%4294967296)/4294967296;
const lons=boundary.map(p=>p[0]),lats=boundary.map(p=>p[1]);
const point=()=>[Math.min(...lons)+random()*(Math.max(...lons)-Math.min(...lons)),Math.min(...lats)+random()*(Math.max(...lats)-Math.min(...lats))];
const origin=graph.nodos[0].coordenadas;
const cases=[];
for(let i=0;i<60;i++){
 const from=point(),to=point(),user=point(),result=shortestRoute(graph,from,to);
 const route=G.routeCoordinates(graph,result.ids);
 const near=G.fromLocalMetres([random()*8-4,random()*8-4],route[Math.floor(random()*route.length)]);
 const item={from,to,ids:result.ids,distance:result.distance,visited:result.nodesVisited,users:[]};
 for(const u of [user,near,route[0]]){
  const path=G.guidePath(route,u,origin,{rangeMeters:40,mergeMeters:6});
  item.users.push({user:u,progress:G.routeProgress(route,u,{waypointLookaheadMeters:12,rejoinMeters:8}),path,samples:G.samplePath(path,1),
   inside:isWithinBoundary(u,boundary),toBoundary:G.distanceToBoundaryMeters(u,boundary),local:G.toLocalMetres(u,origin),bearing:G.bearingDegrees(origin,u),direct:distanceMetres(origin,u)});
 }
 cases.push(item);
}
const numbers=[0,.4,.5,1.5,2.5,12.49,49.5,50,52.5,57.5,59.9,60,72,73,144.1,1234.5,-200,-180,-25,24.9,25,69,70,134,135,180,181,540];
const texts=numbers.map(n=>({n,distance:G.formatDistance(n),time:G.walkingTime(Math.abs(n)),turn:G.turnInstruction(n),signed:G.signedAngle(n),arrival:G.arrivalThreshold(n,{arrivalMinMeters:15,arrivalMaxMeters:30})}));
// Ubicación: un paseo que cruza el perímetro, con precisiones y pausas variadas.
const L=await load('js/location-state.js');
const config={maxAccuracyForGuidanceMeters:65,staleAfterMs:30000,boundaryMarginMeters:12,maxBoundaryMarginMeters:40,exitConfirmMs:6000,exitConfirmReadings:3,enterConfirmMs:3000,enterConfirmReadings:2};
const centre=boundary.reduce((sum,p)=>[sum[0]+p[0]/boundary.length,sum[1]+p[1]/boundary.length],[0,0]);
const location=[];
let state=L.initialLocationState(),now=1e12,track=null;
for(let i=0;i<1500;i++){
 now+=Math.floor(random()*(random()<.03?45000:4000));
 if(random()<.3){state=L.tickLocation(state,config,now);location.push({op:'tick',now,guidance:L.guidanceStatus(state,config),zone:state.zone});continue}
 const angle=i/40,radius=120+90*Math.sin(i/23)+random()*30;
 const coordinate=G.fromLocalMetres([Math.cos(angle)*radius,Math.sin(angle)*radius],centre);
 const accuracy=random()<.1?80+random()*100:3+random()*40,timestamp=now-(random()<.05?40000:Math.floor(random()*2000));
 state=L.applyReading(state,{coordinate,accuracy,timestamp},boundary,config,now);
 track=G.updateCourse(track,coordinate,accuracy,{courseMinMeters:5,courseMaxAccuracyMeters:20});
 location.push({op:'reading',now,coordinate,accuracy,timestamp,guidance:L.guidanceStatus(state,config),zone:state.zone,course:track?.course??null});
}
writeFileSync(new URL('esperado.json',import.meta.url),JSON.stringify({origin,cases,texts,location}));
console.log(`OK: ${cases.length} rutas, ${texts.length} textos y ${location.length} pasos de ubicación en esperado.json (zonas: ${[...new Set(location.map(step=>step.guidance))]})`);

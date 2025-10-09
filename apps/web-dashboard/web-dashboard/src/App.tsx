import { useSignalR } from "./lib/useSignalR";
function App() {
  useSignalR("http://localhost:8080/hub/alerts", (ev)=>console.log("INCIDENT", ev));
  return <div className="p-6">SSSP Dashboard</div>;
}
export default App;

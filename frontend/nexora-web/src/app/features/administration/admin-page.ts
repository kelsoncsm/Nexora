import { Component, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { AdministrationService, AdminDashboard, AdminTenant, AdminUser, Segment, AuditLog, Feature, Plan } from './administration.service';

type Section = 'dashboard' | 'tenants' | 'users' | 'segments' | 'features' | 'plans' | 'audit';

@Component({
  selector: 'app-admin-page',
  template: `
    <div class="admin-shell">
      <aside><span class="brand">Nexora</span><strong>Platform Admin</strong>
        <a class="nav-link" href="/admin/assinaturas">Assinaturas</a>
        <a class="nav-link" href="/admin/billing">Preços e invoices</a>
        <a class="nav-link" href="/admin/relatorios">Relatórios</a>
        @for (item of menu; track item.key) { <button [class.active]="section() === item.key" (click)="open(item.key)">{{ item.label }}</button> }
      </aside>
      <main><header><p>Administração global</p><h1>{{ title() }}</h1></header>
        @if (loading()) { <p>Carregando...</p> } @else if (error()) { <p class="error">{{ error() }}</p> }
        @else if (section() === 'dashboard' && dashboard()) {
          <div class="cards"><article><b>{{ dashboard()!.totalTenants }}</b><span>Tenants</span></article><article><b>{{ dashboard()!.activeTenants }}</b><span>Ativos</span></article><article><b>{{ dashboard()!.totalUsers }}</b><span>Usuários</span></article><article><b>{{ dashboard()!.activeSegments }}</b><span>Segmentos</span></article></div>
        } @else {
          @if(section()==='features'||section()==='plans'){<form (submit)="create($event)"><input name="code" placeholder="Código" #code><input name="name" placeholder="Nome" #name><button type="submit" (click)="draftCode=code.value;draftName=name.value">Adicionar</button></form>}
          <div class="table"><div class="row head"><span>Identificador</span><span>Nome / ação</span><span>Status / data</span></div>
          @for (row of rows(); track row.id) { <div class="row"><span>{{ identifier(row) }}</span><span>{{ description(row) }}</span><span>{{ detail(row) }} @if(isPlan(row)){<button type="button" (click)="togglePlan(row,'public')">{{row.isPublic?'Ocultar':'Publicar'}}</button><button type="button" (click)="togglePlan(row,'trial')">{{row.isTrialEligible?'Remover trial':'Permitir trial'}}</button>}</span></div> }</div>
        }
      </main>
    </div>`,
  styles: [`:host{display:block;min-height:100vh;background:#f4f7fb;color:#162033}.admin-shell{display:grid;grid-template-columns:240px 1fr;min-height:100vh}aside{background:#101c33;color:white;padding:2rem 1.25rem;display:flex;flex-direction:column;gap:.5rem}.brand{color:#78e2c2;font-size:1.4rem}aside strong{margin-bottom:2rem}.nav-link,button{border:0;background:transparent;color:#bac6dc;padding:.8rem;text-align:left;border-radius:.6rem;cursor:pointer;font:inherit;text-decoration:none}.nav-link:hover,button.active,button:hover{background:#233454;color:white}main{padding:2rem clamp(1rem,4vw,4rem)}header p{color:#5271a8;margin:0}h1{margin:.25rem 0 2rem}.cards{display:grid;grid-template-columns:repeat(4,minmax(130px,1fr));gap:1rem}.cards article,.table{background:white;border:1px solid #e1e7f0;border-radius:.8rem;box-shadow:0 8px 30px #17345d0d}.cards article{padding:1.5rem;display:flex;flex-direction:column}.cards b{font-size:2rem}.cards span{color:#65738b}.row{display:grid;grid-template-columns:1fr 2fr 1fr;padding:1rem;border-bottom:1px solid #edf0f5;gap:1rem}.head{font-weight:700;background:#f9fafc}.error{color:#a61b32}@media(max-width:760px){.admin-shell{grid-template-columns:1fr}aside{position:static;flex-direction:row;flex-wrap:wrap;padding:1rem}.brand,aside strong{width:100%;margin:0}.cards{grid-template-columns:1fr 1fr}.row{grid-template-columns:1fr}.head{display:none}}`]
})
export class AdminPage {
  private readonly api = inject(AdministrationService);
  readonly section = signal<Section>('dashboard'); readonly loading = signal(false); readonly error = signal('');
  readonly dashboard = signal<AdminDashboard | null>(null); readonly rows = signal<(AdminTenant | AdminUser | Segment | AuditLog | Feature | Plan)[]>([]); draftCode='';draftName='';
  readonly menu: { key: Section; label: string }[] = [{key:'dashboard',label:'Dashboard'},{key:'tenants',label:'Tenants'},{key:'users',label:'Users'},{key:'segments',label:'Segments'},{key:'features',label:'Features'},{key:'plans',label:'Plans'},{key:'audit',label:'Audit Logs'}];
  constructor() { this.open('dashboard'); }
  title() { return this.menu.find(x => x.key === this.section())?.label ?? ''; }
  open(section: Section) { this.section.set(section); this.loading.set(true); this.error.set('');
    const request: Observable<unknown> = section === 'dashboard' ? this.api.dashboard() : section === 'tenants' ? this.api.tenants() : section === 'users' ? this.api.users() : section === 'segments' ? this.api.segments() : section==='features'?this.api.features():section==='plans'?this.api.plans():this.api.auditLogs();
    request.subscribe({ next: value => { if (section === 'dashboard') this.dashboard.set(value as AdminDashboard); else this.rows.set(value as never[]); this.loading.set(false); }, error: () => { this.error.set('Não foi possível carregar esta seção.'); this.loading.set(false); } });
  }
  create(event:Event){event.preventDefault();const request=this.section()==='features'?this.api.createFeature(this.draftCode,this.draftName):this.api.createPlan(this.draftCode,this.draftName);request.subscribe({next:()=>this.open(this.section()),error:()=>this.error.set('Não foi possível salvar.')});}
  identifier(row: any) { return row.code ?? row.slug ?? row.email ?? row.targetType; }
  description(row: any) { return row.name ?? row.email ?? row.action; }
  detail(row: any) { return typeof row.isActive === 'boolean' ? (row.isActive ? 'Ativo' : 'Inativo') : new Date(row.occurredAt).toLocaleString('pt-BR'); }
  isPlan(row:AdminTenant|AdminUser|Segment|AuditLog|Feature|Plan):row is Plan{return 'features' in row;}
  togglePlan(plan:Plan,field:'public'|'trial'):void{const isPublic=field==='public'?!plan.isPublic:plan.isPublic;const trial=field==='trial'?!plan.isTrialEligible:plan.isTrialEligible;this.api.configurePlan(plan,isPublic,trial).subscribe({next:()=>this.open('plans'),error:()=>this.error.set('Não foi possível configurar o plano.')});}
}

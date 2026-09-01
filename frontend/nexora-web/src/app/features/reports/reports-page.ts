import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { APP_CONFIG } from '../../core/config/app-config';

interface ReportData {
  totalTenants?: number; activeTenants?: number; trialSubscriptions?: number; activeSubscriptions?: number; pastDueSubscriptions?: number;
  customers?: number; appointments?: { total: number; completed: number; cancelled: number; noShow: number };
  paidRevenue?: number; currency?: string; productivity?: { professionalName: string; completedAppointments: number }[];
}

@Component({
  selector: 'app-reports-page', imports: [FormsModule],
  template: `<main><header><a href="/">Nexora</a><div><span>Relatórios</span><h1>{{ platform ? 'Visão da plataforma' : 'Visão operacional' }}</h1></div></header>
    <form (ngSubmit)="load()"><label>De <input type="date" name="from" [(ngModel)]="from"></label><label>Até <input type="date" name="to" [(ngModel)]="to"></label><button>Atualizar</button></form>
    @if(error()){<p class="error">{{error()}}</p>} @if(data();as report){<section class="cards">
      @if(platform){<article><b>{{report.totalTenants}}</b><span>Tenants</span></article><article><b>{{report.activeTenants}}</b><span>Tenants ativos</span></article><article><b>{{report.trialSubscriptions}}</b><span>Trials</span></article><article><b>{{report.pastDueSubscriptions}}</b><span>Past due</span></article>}
      @else{<article><b>{{report.customers}}</b><span>Clientes</span></article><article><b>{{report.appointments?.total}}</b><span>Agendamentos</span></article><article><b>{{report.appointments?.completed}}</b><span>Concluídos</span></article><article><b>{{report.appointments?.noShow}}</b><span>No-show</span></article>}
      @if(platform && report.paidRevenue !== undefined && report.currency){<article><b>{{report.paidRevenue.toLocaleString('pt-BR',{style:'currency',currency:report.currency})}}</b><span>Receita SaaS paga</span></article>}
    </section>@if(report.productivity?.length){<section class="table"><h2>Produtividade</h2>@for(item of report.productivity;track item.professionalName){<div><span>{{item.professionalName}}</span><b>{{item.completedAppointments}}</b></div>}</section>}}
  </main>`,
  styles:[`:host{display:block;min-height:100vh;background:#f4f7fb;color:#172033}main{max-width:1120px;margin:auto;padding:2rem}header{display:flex;gap:2rem;align-items:center}header a{font-weight:800;color:#285cdb;text-decoration:none}h1{margin:.2rem 0 1.5rem}form{display:flex;gap:1rem;align-items:end;background:white;padding:1rem;border-radius:.8rem}label{display:grid;gap:.35rem}input,button{padding:.7rem;border:1px solid #ccd6e6;border-radius:.5rem}button{background:#285cdb;color:white}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:1rem;margin-top:1rem}.cards article,.table{background:white;padding:1.3rem;border-radius:.8rem;border:1px solid #e1e7f0}.cards b{display:block;font-size:1.7rem}.cards span{color:#64728a}.table{margin-top:1rem}.table div{display:flex;justify-content:space-between;padding:.6rem 0;border-bottom:1px solid #edf0f5}.error{color:#a61b32}`]
})
export class ReportsPage {
  private http=inject(HttpClient);private config=inject(APP_CONFIG);platform=inject(ActivatedRoute).snapshot.data['platform']===true;
  from=new Date(Date.now()-30*86400000).toISOString().slice(0,10);to=new Date(Date.now()+86400000).toISOString().slice(0,10);data=signal<ReportData|null>(null);error=signal('');
  constructor(){this.load()} load(){const path=this.platform?'/admin/reports/overview':'/reports/overview';this.http.get<ReportData>(`${this.config.apiBaseUrl}${path}?from=${this.from}T00:00:00Z&to=${this.to}T00:00:00Z`).subscribe({next:x=>{this.data.set(x);this.error.set('')},error:()=>this.error.set('Não foi possível carregar os indicadores.')});}
}

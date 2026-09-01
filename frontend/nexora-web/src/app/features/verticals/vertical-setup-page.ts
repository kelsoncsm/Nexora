import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { APP_CONFIG } from '../../core/config/app-config';

interface Preset { code:string; name:string; category:string; selected?:boolean; durationMinutes?:number; price?:number }
interface Setup { code:string; name:string; onboardingTitle:string; onboardingDescription:string; dashboardTitle:string; servicePresets:Preset[] }

@Component({selector:'app-vertical-setup-page',imports:[FormsModule],template:`
<main>@if(loading()){<section class="card"><p>Carregando configuração…</p></section>}@else if(setup();as model){
  <header><span class="nx-eyebrow">{{model.name}}</span><h1>{{model.onboardingTitle}}</h1><p>{{model.onboardingDescription}}</p></header>
  <section class="card"><h2>Serviços iniciais</h2><p>Todos são opcionais. Ao selecionar um item, informe os valores praticados pela sua empresa.</p>
  <div class="presets">@for(item of model.servicePresets;track item.code){<article [class.selected]="item.selected">
    <label><input type="checkbox" [(ngModel)]="item.selected"> <span><b>{{item.name}}</b><small>{{item.category}}</small></span></label>
    @if(item.selected){<div><label>Duração (min)<input type="number" min="1" [(ngModel)]="item.durationMinutes"></label><label>Preço<input type="number" min="0" step=".01" [(ngModel)]="item.price"></label></div>}
  </article>}</div>@if(error()){<p class="nx-error" role="alert">{{error()}}</p>}<footer><button class="secondary" (click)="skip()">Configurar depois</button><button [disabled]="busy()||!valid()" (click)="apply()">{{busy()?'Salvando…':'Iniciar operação'}}</button></footer></section>
}@else{<section class="card"><h1>Configuração genérica</h1><p>Este segmento não possui um template inicial. Você pode configurar serviços normalmente.</p><button (click)="skip()">Continuar</button></section>}</main>`,styles:[`
:host{display:block;min-height:100vh;background:var(--nx-bg)}main{width:min(100% - 28px,900px);margin:auto;padding:32px 0}header{text-align:center;margin-bottom:24px}h1{font-size:clamp(30px,5vw,48px);letter-spacing:-.04em;margin:.4rem}header p,.card>p{color:var(--nx-muted)}.card{padding:clamp(22px,5vw,42px);background:#fff;border:1px solid var(--nx-border);border-radius:22px;box-shadow:var(--nx-shadow)}.presets{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}.presets article{padding:16px;border:2px solid var(--nx-border);border-radius:14px}.presets article.selected{border-color:var(--nx-primary);background:var(--nx-primary-soft)}article>label{display:flex;gap:10px}article span{display:grid}small{color:var(--nx-muted)}article>div{display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-top:14px}article>div label{display:grid;gap:5px;font-size:.8rem;font-weight:700}input[type=number]{width:100%;padding:10px;border:1px solid var(--nx-border);border-radius:8px}footer{display:flex;justify-content:flex-end;gap:10px;margin-top:24px}button{padding:11px 18px;border:0;border-radius:10px;background:var(--nx-primary);color:#fff;font-weight:800}.secondary{background:#eef0f6;color:var(--nx-text)}button:disabled{opacity:.55}@media(max-width:640px){.presets{grid-template-columns:1fr}}
`]})
export class VerticalSetupPage {
  private http=inject(HttpClient); private config=inject(APP_CONFIG); private router=inject(Router);
  readonly setup=signal<Setup|null>(null); readonly loading=signal(true); readonly busy=signal(false); readonly error=signal('');
  constructor(){this.http.get<Setup>(`${this.config.apiBaseUrl}/vertical-setup`).subscribe({next:x=>{this.setup.set(x);this.loading.set(false)},error:()=>this.loading.set(false)})}
  valid(){return this.setup()?.servicePresets.filter(x=>x.selected).every(x=>(x.durationMinutes??0)>0&&(x.price??-1)>=0)??true}
  apply(){const model=this.setup();if(!model||!this.valid())return;this.busy.set(true);this.http.post(`${this.config.apiBaseUrl}/vertical-setup/apply`,{services:model.servicePresets.filter(x=>x.selected).map(x=>({presetCode:x.code,durationMinutes:+x.durationMinutes!,price:+x.price!}))}).subscribe({next:()=>this.skip(),error:e=>{this.busy.set(false);this.error.set(e.error?.title??'Não foi possível aplicar a configuração.')}})}
  skip(){void this.router.navigateByUrl('/clientes')}
}

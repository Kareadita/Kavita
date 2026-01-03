import {ChangeDetectionStrategy, Component, inject, OnInit, signal} from '@angular/core';
import {DeviceService} from "../../_services/device.service";
import {ClientDevice} from "../../_models/client-device";
import {ClientDeviceCardComponent} from "../../_single-module/client-device-card/client-device-card.component";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {StatisticsService} from "../../_services/statistics.service";
import {ClientDeviceClientTypePipe} from "../../_pipes/client-device-client-type.pipe";
import {ClientDeviceTypePipe} from "../../_pipes/client-device-type.pipe";
import {PieChartComponent} from "../../shared/_charts/pie-chart/pie-chart.component";
import {StatCount} from "../../statistics/_models/stat-count";

@Component({
  selector: 'app-server-devices',
  imports: [
    ClientDeviceCardComponent,
    LoadingComponent,
    TranslocoDirective,
    PieChartComponent
  ],
  templateUrl: './server-devices.component.html',
  styleUrl: './server-devices.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ServerDevicesComponent implements OnInit {

  private readonly deviceService = inject(DeviceService);
  private readonly statsService = inject(StatisticsService);

  private readonly clientDeviceClientTypePipe = new ClientDeviceClientTypePipe();
  private readonly clientDeviceTypePipe = new ClientDeviceTypePipe();

  clientDevices = signal<ClientDevice[]>([]);
  clientDeviceTypeBreakdown = signal<StatCount<number>[]>([]);
  mobileVsDesktop = signal<StatCount<string>[]>([]);

  ngOnInit() {
    this.loadDevices();

    this.statsService.getClientDeviceBreakdown().subscribe(clientDeviceBreakdown => {
      this.clientDeviceTypeBreakdown.set(clientDeviceBreakdown.records);
    });

    this.statsService.getClientDeviceTypeCounts().subscribe(data => {
      this.mobileVsDesktop.set(data);
    });
  }

  loadDevices() {
    this.deviceService.getAllDevices().subscribe(devices => {
      this.clientDevices.set([...devices]);
    });

  }

  clientDeviceClientTypeTransformer(r: StatCount<number>) {
    return this.clientDeviceClientTypePipe.transform(r.value);
  }

  clientDeviceTypeTransformer(r: StatCount<string>) {
    return this.clientDeviceTypePipe.transform(r.value);
  }

  deviceDeleted(id: number) {
    this.clientDevices.update(x => [...x.filter(d => d.id != id)])
  }


}

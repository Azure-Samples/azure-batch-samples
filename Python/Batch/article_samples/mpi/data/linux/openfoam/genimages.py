# flake8: noqa
try:
    paraview.simple
except:
    from paraview.simple import *
paraview.simple._DisableFirstRenderCameraReset()

motorBike_chain40_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_chain40_0.vtk'])
motorBike_clutchhousing52_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_clutch-housing52_0.vtk'])
motorBike_dialholder44_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_dial-holder44_0.vtk'])
motorBike_driversseat28_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_driversseat28_0.vtk'])
motorBike_engine56_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_engine56_0.vtk'])
motorBike_exhaust31_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_exhaust31_0.vtk'])
motorBike_fairinginnerplate51_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_fairing-inner-plate51_0.vtk'])
motorBike_footpeg60_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_footpeg60_0.vtk'])
motorBike_frbrakecaliper36_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_fr-brake-caliper36_0.vtk'])
motorBike_frforks39_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_fr-forks39_0.vtk'])
motorBike_frmudguardshadow81_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_fr-mud-guard-shadow81_0.vtk'])
motorBike_frmudguard33_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_fr-mud-guard33_0.vtk'])
motorBike_frwhbrakediskshadow83_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_fr-wh-brake-disk-shadow83_0.vtk'])
motorBike_frwhbrakedisk01212_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_fr-wh-brake-disk01212_0.vtk'])
motorBike_frwhbrakedisk35_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_fr-wh-brake-disk35_0.vtk'])
motorBike_frwhrim01111_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_fr-wh-rim01111_0.vtk'])
motorBike_frwhrim34_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_fr-wh-rim34_0.vtk'])
motorBike_frwhtyre37_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_fr-wh-tyre37_0.vtk'])
motorBike_frame016shadow13_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_frame016-shadow13_0.vtk'])
motorBike_frame01616_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_frame01616_0.vtk'])
motorBike_frame070_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_frame070_0.vtk'])
motorBike_frame48_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_frame48_0.vtk'])
motorBike_frtfairing001shadow74_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_frt-fairing001-shadow74_0.vtk'])
motorBike_frtfairing0011_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_frt-fairing0011_0.vtk'])
motorBike_frtfairing25_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_frt-fairing25_0.vtk'])
motorBike_fueltank30_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_fuel-tank30_0.vtk'])
motorBike_hbars38_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_hbars38_0.vtk'])
motorBike_headlights27_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_headlights27_0.vtk'])
motorBike_radiatorshadow86_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_radiator-shadow86_0.vtk'])
motorBike_radiator53_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_radiator53_0.vtk'])
motorBike_rearbody29_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rear-body29_0.vtk'])
motorBike_rearbrakecaliper62_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rear-brake-caliper62_0.vtk'])
motorBike_rearbrakefluidpotbracketshadow88_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rear-brake-fluid-pot-bracket-shadow88_0.vtk'])
motorBike_rearbrakefluidpotbracket58_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rear-brake-fluid-pot-bracket58_0.vtk'])
motorBike_rearbrakefluidpot59_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rear-brake-fluid-pot59_0.vtk'])
motorBike_rearbrakelights46_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rear-brake-lights46_0.vtk'])
motorBike_rearlightbracket47_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rear-light-bracket47_0.vtk'])
motorBike_rearmudguardshadow84_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rear-mud-guard-shadow84_0.vtk'])
motorBike_rearmudguard49_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rear-mud-guard49_0.vtk'])
motorBike_rearshocklinkshadow87_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rear-shock-link-shadow87_0.vtk'])
motorBike_rearshocklink57_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rear-shock-link57_0.vtk'])
motorBike_rearsuspspringdampshadow85_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rear-susp-spring-damp-shadow85_0.vtk'])
motorBike_rearsuspspringdamp50_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rear-susp-spring-damp50_0.vtk'])
motorBike_rearsusp014shadow15_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rear-susp014-shadow15_0.vtk'])
motorBike_rearsusp01414_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rear-susp01414_0.vtk'])
motorBike_rearsusp45_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rear-susp45_0.vtk'])
motorBike_rearseat24_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rearseat24_0.vtk'])
motorBike_riderbody69_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rider-body69_0.vtk'])
motorBike_riderboots67_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rider-boots67_0.vtk'])
motorBike_ridergloves68_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rider-gloves68_0.vtk'])
motorBike_riderhelmet65_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rider-helmet65_0.vtk'])
motorBike_ridervisor66_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rider-visor66_0.vtk'])
motorBike_rounddial43_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_round-dial43_0.vtk'])
motorBike_rrwhchainhubshadow89_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rr-wh-chain-hub-shadow89_0.vtk'])
motorBike_rrwhchainhub02222_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rr-wh-chain-hub02222_0.vtk'])
motorBike_rrwhchainhub61_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rr-wh-chain-hub61_0.vtk'])
motorBike_rrwhrim005shadow17_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rr-wh-rim005-shadow17_0.vtk'])
motorBike_rrwhrim0055_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rr-wh-rim0055_0.vtk'])
motorBike_rrwhrim01010_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rr-wh-rim01010_0.vtk'])
motorBike_rrwhrim32_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rr-wh-rim32_0.vtk'])
motorBike_rrwhtyre41_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_rr-wh-tyre41_0.vtk'])
motorBike_squaredial42_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_square-dial42_0.vtk'])
motorBike_waterpipe54_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_water-pipe54_0.vtk'])
motorBike_waterpump55_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_water-pump55_0.vtk'])
motorBike_windshieldshadow75_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_windshield-shadow75_0.vtk'])
motorBike_windshield0022_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_windshield0022_0.vtk'])
motorBike_windshield26_0_vtk = LegacyVTKReader(
    FileNames=['motorBike_windshield26_0.vtk'])

RenderView1 = GetRenderView()
SetActiveSource(motorBike_rearsusp01414_0_vtk)
DataRepresentation113 = Show()
DataRepresentation113.ScaleFactor = 0.035687534511089324
DataRepresentation113.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation113.SelectionPointFieldDataArrayName = 'k'
DataRepresentation113.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rrwhrim01010_0_vtk)
DataRepresentation114 = Show()
DataRepresentation114.ScaleFactor = 0.04559381008148194
DataRepresentation114.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation114.SelectionPointFieldDataArrayName = 'k'
DataRepresentation114.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frtfairing0011_0_vtk)
DataRepresentation115 = Show()
DataRepresentation115.ScaleFactor = 0.10810967292636633
DataRepresentation115.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation115.SelectionPointFieldDataArrayName = 'k'
DataRepresentation115.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_squaredial42_0_vtk)
DataRepresentation116 = Show()
DataRepresentation116.ScaleFactor = 0.004758159350603819
DataRepresentation116.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation116.SelectionPointFieldDataArrayName = 'k'
DataRepresentation116.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frforks39_0_vtk)
DataRepresentation117 = Show()
DataRepresentation117.ScaleFactor = 0.060332491993904114
DataRepresentation117.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation117.SelectionPointFieldDataArrayName = 'k'
DataRepresentation117.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_fueltank30_0_vtk)
DataRepresentation118 = Show()
DataRepresentation118.ScaleFactor = 0.05498323738574982
DataRepresentation118.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation118.SelectionPointFieldDataArrayName = 'k'
DataRepresentation118.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rearmudguardshadow84_0_vtk)
DataRepresentation119 = Show()
DataRepresentation119.ScaleFactor = 0.03839125633239746
DataRepresentation119.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation119.SelectionPointFieldDataArrayName = 'k'
DataRepresentation119.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_riderboots67_0_vtk)
DataRepresentation120 = Show()
DataRepresentation120.ScaleFactor = 0.06220180690288544
DataRepresentation120.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation120.SelectionPointFieldDataArrayName = 'k'
DataRepresentation120.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_radiatorshadow86_0_vtk)
DataRepresentation121 = Show()
DataRepresentation121.ScaleFactor = 0.027198296785354615
DataRepresentation121.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation121.SelectionPointFieldDataArrayName = 'k'
DataRepresentation121.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frwhrim01111_0_vtk)
DataRepresentation122 = Show()
DataRepresentation122.ScaleFactor = 0.04614693969488144
DataRepresentation122.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation122.SelectionPointFieldDataArrayName = 'k'
DataRepresentation122.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_clutchhousing52_0_vtk)
DataRepresentation123 = Show()
DataRepresentation123.ScaleFactor = 0.04952635169029236
DataRepresentation123.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation123.SelectionPointFieldDataArrayName = 'k'
DataRepresentation123.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_hbars38_0_vtk)
DataRepresentation124 = Show()
DataRepresentation124.ScaleFactor = 0.06647889614105225
DataRepresentation124.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation124.SelectionPointFieldDataArrayName = 'k'
DataRepresentation124.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frtfairing25_0_vtk)
DataRepresentation125 = Show()
DataRepresentation125.ScaleFactor = 0.05382243096828461
DataRepresentation125.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation125.SelectionPointFieldDataArrayName = 'k'
DataRepresentation125.SelectionCellFieldDataArrayName = 'k'

RenderView1.CenterOfRotation = [0.9768998324871063, -0.0006405040621757507,
                                0.4527733325958252]

SetActiveSource(motorBike_windshield26_0_vtk)
DataRepresentation126 = Show()
DataRepresentation126.ScaleFactor = 0.03235955983400345
DataRepresentation126.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation126.SelectionPointFieldDataArrayName = 'k'
DataRepresentation126.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rearbody29_0_vtk)
DataRepresentation127 = Show()
DataRepresentation127.ScaleFactor = 0.07774371504783631
DataRepresentation127.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation127.SelectionPointFieldDataArrayName = 'k'
DataRepresentation127.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frwhbrakedisk35_0_vtk)
DataRepresentation128 = Show()
DataRepresentation128.ScaleFactor = 0.02972700595855713
DataRepresentation128.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation128.SelectionPointFieldDataArrayName = 'k'
DataRepresentation128.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rearshocklinkshadow87_0_vtk)
DataRepresentation129 = Show()
DataRepresentation129.ScaleFactor = 0.008941906690597535
DataRepresentation129.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation129.SelectionPointFieldDataArrayName = 'k'
DataRepresentation129.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_fairinginnerplate51_0_vtk)
DataRepresentation130 = Show()
DataRepresentation130.ScaleFactor = 0.05382243096828461
DataRepresentation130.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation130.SelectionPointFieldDataArrayName = 'k'
DataRepresentation130.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frwhrim34_0_vtk)
DataRepresentation131 = Show()
DataRepresentation131.ScaleFactor = 0.042638809978961946
DataRepresentation131.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation131.SelectionPointFieldDataArrayName = 'k'
DataRepresentation131.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rrwhchainhub02222_0_vtk)
DataRepresentation132 = Show()
DataRepresentation132.ScaleFactor = 0.03466259241104126
DataRepresentation132.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation132.SelectionPointFieldDataArrayName = 'k'
DataRepresentation132.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rearbrakefluidpot59_0_vtk)
DataRepresentation133 = Show()
DataRepresentation133.ScaleFactor = 0.006864506006240845
DataRepresentation133.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation133.SelectionPointFieldDataArrayName = 'k'
DataRepresentation133.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rrwhchainhubshadow89_0_vtk)
DataRepresentation134 = Show()
DataRepresentation134.ScaleFactor = 0.020195069909095767
DataRepresentation134.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation134.SelectionPointFieldDataArrayName = 'k'
DataRepresentation134.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frbrakecaliper36_0_vtk)
DataRepresentation135 = Show()
DataRepresentation135.ScaleFactor = 0.021085135638713837
DataRepresentation135.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation135.SelectionPointFieldDataArrayName = 'k'
DataRepresentation135.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_ridergloves68_0_vtk)
DataRepresentation136 = Show()
DataRepresentation136.ScaleFactor = 0.06373784840106965
DataRepresentation136.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation136.SelectionPointFieldDataArrayName = 'k'
DataRepresentation136.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_headlights27_0_vtk)
DataRepresentation137 = Show()
DataRepresentation137.ScaleFactor = 0.048497574031353
DataRepresentation137.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation137.SelectionPointFieldDataArrayName = 'k'
DataRepresentation137.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rearseat24_0_vtk)
DataRepresentation138 = Show()
DataRepresentation138.ScaleFactor = 0.03251786231994629
DataRepresentation138.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation138.SelectionPointFieldDataArrayName = 'k'
DataRepresentation138.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rearbrakelights46_0_vtk)
DataRepresentation139 = Show()
DataRepresentation139.ScaleFactor = 0.017779697477817536
DataRepresentation139.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation139.SelectionPointFieldDataArrayName = 'k'
DataRepresentation139.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rrwhrim0055_0_vtk)
DataRepresentation140 = Show()
DataRepresentation140.ScaleFactor = 0.023816800117492678
DataRepresentation140.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation140.SelectionPointFieldDataArrayName = 'k'
DataRepresentation140.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_ridervisor66_0_vtk)
DataRepresentation141 = Show()
DataRepresentation141.ScaleFactor = 0.02613819241523743
DataRepresentation141.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation141.SelectionPointFieldDataArrayName = 'k'
DataRepresentation141.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_waterpump55_0_vtk)
DataRepresentation142 = Show()
DataRepresentation142.ScaleFactor = 0.009220749139785767
DataRepresentation142.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation142.SelectionPointFieldDataArrayName = 'k'
DataRepresentation142.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rrwhchainhub61_0_vtk)
DataRepresentation143 = Show()
DataRepresentation143.ScaleFactor = 0.020405626296997072
DataRepresentation143.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation143.SelectionPointFieldDataArrayName = 'k'
DataRepresentation143.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_footpeg60_0_vtk)
DataRepresentation144 = Show()
DataRepresentation144.ScaleFactor = 0.05771320462226868
DataRepresentation144.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation144.SelectionPointFieldDataArrayName = 'k'
DataRepresentation144.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frwhbrakediskshadow83_0_vtk)
DataRepresentation145 = Show()
DataRepresentation145.ScaleFactor = 0.029412719607353213
DataRepresentation145.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation145.SelectionPointFieldDataArrayName = 'k'
DataRepresentation145.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frwhtyre37_0_vtk)
DataRepresentation146 = Show()
DataRepresentation146.ScaleFactor = 0.06097606122493744
DataRepresentation146.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation146.SelectionPointFieldDataArrayName = 'k'
DataRepresentation146.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rearshocklink57_0_vtk)
DataRepresentation147 = Show()
DataRepresentation147.ScaleFactor = 0.01191062331199646
DataRepresentation147.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation147.SelectionPointFieldDataArrayName = 'k'
DataRepresentation147.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frwhbrakedisk01212_0_vtk)
DataRepresentation148 = Show()
DataRepresentation148.ScaleFactor = 0.01597747504711151
DataRepresentation148.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation148.SelectionPointFieldDataArrayName = 'k'
DataRepresentation148.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_dialholder44_0_vtk)
DataRepresentation149 = Show()
DataRepresentation149.ScaleFactor = 0.016453153640031814
DataRepresentation149.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation149.SelectionPointFieldDataArrayName = 'k'
DataRepresentation149.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rearsuspspringdamp50_0_vtk)
DataRepresentation150 = Show()
DataRepresentation150.ScaleFactor = 0.014047491550445558
DataRepresentation150.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation150.SelectionPointFieldDataArrayName = 'k'
DataRepresentation150.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rearsusp014shadow15_0_vtk)
DataRepresentation151 = Show()
DataRepresentation151.ScaleFactor = 0.035033150017261504
DataRepresentation151.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation151.SelectionPointFieldDataArrayName = 'k'
DataRepresentation151.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rearbrakefluidpotbracketshadow88_0_vtk)
DataRepresentation152 = Show()
DataRepresentation152.ScaleFactor = 0.008681225776672364
DataRepresentation152.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation152.SelectionPointFieldDataArrayName = 'k'
DataRepresentation152.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_riderbody69_0_vtk)
DataRepresentation153 = Show()
DataRepresentation153.ScaleFactor = 0.08006701767444611
DataRepresentation153.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation153.SelectionPointFieldDataArrayName = 'k'
DataRepresentation153.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rearlightbracket47_0_vtk)
DataRepresentation154 = Show()
DataRepresentation154.ScaleFactor = 0.020413921028375626
DataRepresentation154.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation154.SelectionPointFieldDataArrayName = 'k'
DataRepresentation154.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frame070_0_vtk)
DataRepresentation155 = Show()
DataRepresentation155.ScaleFactor = 0.013001590967178345
DataRepresentation155.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation155.SelectionPointFieldDataArrayName = 'k'
DataRepresentation155.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frame48_0_vtk)
DataRepresentation156 = Show()
DataRepresentation156.ScaleFactor = 0.08575724959373475
DataRepresentation156.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation156.SelectionPointFieldDataArrayName = 'k'
DataRepresentation156.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_waterpipe54_0_vtk)
DataRepresentation157 = Show()
DataRepresentation157.ScaleFactor = 0.02095581591129303
DataRepresentation157.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation157.SelectionPointFieldDataArrayName = 'k'
DataRepresentation157.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frtfairing001shadow74_0_vtk)
DataRepresentation158 = Show()
DataRepresentation158.ScaleFactor = 0.10765927247703076
DataRepresentation158.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation158.SelectionPointFieldDataArrayName = 'k'
DataRepresentation158.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frmudguardshadow81_0_vtk)
DataRepresentation159 = Show()
DataRepresentation159.ScaleFactor = 0.04745462983846665
DataRepresentation159.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation159.SelectionPointFieldDataArrayName = 'k'
DataRepresentation159.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rrwhtyre41_0_vtk)
DataRepresentation160 = Show()
DataRepresentation160.ScaleFactor = 0.06201939582824707
DataRepresentation160.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation160.SelectionPointFieldDataArrayName = 'k'
DataRepresentation160.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_chain40_0_vtk)
DataRepresentation161 = Show()
DataRepresentation161.ScaleFactor = 0.08140577077865602
DataRepresentation161.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation161.SelectionPointFieldDataArrayName = 'k'
DataRepresentation161.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rrwhrim32_0_vtk)
DataRepresentation162 = Show()
DataRepresentation162.ScaleFactor = 0.04235827922821045
DataRepresentation162.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation162.SelectionPointFieldDataArrayName = 'k'
DataRepresentation162.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rrwhrim005shadow17_0_vtk)
DataRepresentation163 = Show()
DataRepresentation163.ScaleFactor = 0.023816800117492678
DataRepresentation163.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation163.SelectionPointFieldDataArrayName = 'k'
DataRepresentation163.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frame016shadow13_0_vtk)
DataRepresentation164 = Show()
DataRepresentation164.ScaleFactor = 0.05108032524585724
DataRepresentation164.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation164.SelectionPointFieldDataArrayName = 'k'
DataRepresentation164.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rounddial43_0_vtk)
DataRepresentation165 = Show()
DataRepresentation165.ScaleFactor = 0.007233957864809782
DataRepresentation165.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation165.SelectionPointFieldDataArrayName = 'k'
DataRepresentation165.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frmudguard33_0_vtk)
DataRepresentation166 = Show()
DataRepresentation166.ScaleFactor = 0.04750459045171738
DataRepresentation166.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation166.SelectionPointFieldDataArrayName = 'k'
DataRepresentation166.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_exhaust31_0_vtk)
DataRepresentation167 = Show()
DataRepresentation167.ScaleFactor = 0.1309053659439087
DataRepresentation167.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation167.SelectionPointFieldDataArrayName = 'k'
DataRepresentation167.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rearsuspspringdampshadow85_0_vtk)
DataRepresentation168 = Show()
DataRepresentation168.ScaleFactor = 0.013828927278518678
DataRepresentation168.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation168.SelectionPointFieldDataArrayName = 'k'
DataRepresentation168.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rearbrakecaliper62_0_vtk)
DataRepresentation169 = Show()
DataRepresentation169.ScaleFactor = 0.012679851055145264
DataRepresentation169.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation169.SelectionPointFieldDataArrayName = 'k'
DataRepresentation169.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_engine56_0_vtk)
DataRepresentation170 = Show()
DataRepresentation170.ScaleFactor = 0.050237254798412324
DataRepresentation170.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation170.SelectionPointFieldDataArrayName = 'k'
DataRepresentation170.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rearbrakefluidpotbracket58_0_vtk)
DataRepresentation171 = Show()
DataRepresentation171.ScaleFactor = 0.008731448650360107
DataRepresentation171.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation171.SelectionPointFieldDataArrayName = 'k'
DataRepresentation171.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_windshield0022_0_vtk)
DataRepresentation172 = Show()
DataRepresentation172.ScaleFactor = 0.018221083283424377
DataRepresentation172.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation172.SelectionPointFieldDataArrayName = 'k'
DataRepresentation172.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rearsusp45_0_vtk)
DataRepresentation173 = Show()
DataRepresentation173.ScaleFactor = 0.06740430593490601
DataRepresentation173.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation173.SelectionPointFieldDataArrayName = 'k'
DataRepresentation173.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_rearmudguard49_0_vtk)
DataRepresentation174 = Show()
DataRepresentation174.ScaleFactor = 0.03839125633239746
DataRepresentation174.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation174.SelectionPointFieldDataArrayName = 'k'
DataRepresentation174.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_driversseat28_0_vtk)
DataRepresentation175 = Show()
DataRepresentation175.ScaleFactor = 0.054681104421615605
DataRepresentation175.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation175.SelectionPointFieldDataArrayName = 'k'
DataRepresentation175.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_radiator53_0_vtk)
DataRepresentation176 = Show()
DataRepresentation176.ScaleFactor = 0.02812890410423279
DataRepresentation176.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation176.SelectionPointFieldDataArrayName = 'k'
DataRepresentation176.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_frame01616_0_vtk)
DataRepresentation177 = Show()
DataRepresentation177.ScaleFactor = 0.04980889558792115
DataRepresentation177.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation177.SelectionPointFieldDataArrayName = 'k'
DataRepresentation177.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_windshieldshadow75_0_vtk)
DataRepresentation178 = Show()
DataRepresentation178.ScaleFactor = 0.03214336186647415
DataRepresentation178.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation178.SelectionPointFieldDataArrayName = 'k'
DataRepresentation178.SelectionCellFieldDataArrayName = 'k'

SetActiveSource(motorBike_riderhelmet65_0_vtk)
DataRepresentation179 = Show()
DataRepresentation179.ScaleFactor = 0.03502836227416992
DataRepresentation179.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation179.SelectionPointFieldDataArrayName = 'k'
DataRepresentation179.SelectionCellFieldDataArrayName = 'k'

RenderView1.CameraPosition = [-1.9421209496845542, -3.6887202619121937,
                              2.530228543259441]
RenderView1.CameraClippingRange = [2.740336307746011, 7.655502170345274]
RenderView1.CameraFocalPoint = [0.7296394109725952, -0.008860647678375244,
                                0.6756975054740906]
RenderView1.CameraParallelScale = 0.2615728941994812
RenderView1.CenterOfRotation = [0.7296394109725952, -0.008860647678375244,
                                0.6756975054740906]

a1_p_PVLookupTable = GetLookupTableForArray("p", 1, NanColor=[0.25, 0.0, 0.0],
                                            RGBPoints=[-1.704679701870557e+30,
                                                       0.23, 0.299, 0.754,
                                                       8.145575189684537e+29,
                                                       0.706, 0.016, 0.15],
                                            VectorMode='Magnitude',
                                            ColorSpace='Diverging',
                                            ScalarRangeInitialized=1.0)

a1_p_PiecewiseFunction = CreatePiecewiseFunction(
    Points=[0.0, 0.0, 0.5, 0.0, 1.0, 1.0, 0.5, 0.0])

DataRepresentation126.ColorArrayName = 'p'
DataRepresentation126.LookupTable = a1_p_PVLookupTable

GroupDatasets1 = GroupDatasets(
    Input=[motorBike_headlights27_0_vtk, motorBike_fairinginnerplate51_0_vtk,
           motorBike_frtfairing001shadow74_0_vtk, motorBike_fueltank30_0_vtk,
           motorBike_hbars38_0_vtk, motorBike_clutchhousing52_0_vtk,
           motorBike_driversseat28_0_vtk, motorBike_engine56_0_vtk,
           motorBike_exhaust31_0_vtk, motorBike_dialholder44_0_vtk,
           motorBike_frtfairing25_0_vtk, motorBike_chain40_0_vtk,
           motorBike_frtfairing0011_0_vtk, motorBike_radiatorshadow86_0_vtk,
           motorBike_radiator53_0_vtk, motorBike_rearbrakefluidpot59_0_vtk,
           motorBike_frwhbrakedisk35_0_vtk,
           motorBike_rearsusp014shadow15_0_vtk, motorBike_rearbody29_0_vtk,
           motorBike_rearmudguardshadow84_0_vtk, motorBike_frforks39_0_vtk,
           motorBike_frwhbrakedisk01212_0_vtk,
           motorBike_rearlightbracket47_0_vtk, motorBike_rearshocklink57_0_vtk,
           motorBike_rearmudguard49_0_vtk, motorBike_frbrakecaliper36_0_vtk,
           motorBike_frame070_0_vtk, motorBike_footpeg60_0_vtk,
           motorBike_frame48_0_vtk,
           motorBike_rearbrakefluidpotbracketshadow88_0_vtk,
           motorBike_rearbrakefluidpotbracket58_0_vtk,
           motorBike_frwhbrakediskshadow83_0_vtk,
           motorBike_rearbrakelights46_0_vtk,
           motorBike_rearsuspspringdampshadow85_0_vtk,
           motorBike_frwhrim01111_0_vtk, motorBike_rearbrakecaliper62_0_vtk,
           motorBike_rearsusp01414_0_vtk, motorBike_frmudguardshadow81_0_vtk,
           motorBike_frmudguard33_0_vtk, motorBike_frame016shadow13_0_vtk,
           motorBike_frwhrim34_0_vtk, motorBike_frame01616_0_vtk,
           motorBike_rearshocklinkshadow87_0_vtk,
           motorBike_rearsuspspringdamp50_0_vtk, motorBike_frwhtyre37_0_vtk,
           motorBike_riderbody69_0_vtk, motorBike_squaredial42_0_vtk,
           motorBike_windshieldshadow75_0_vtk, motorBike_rounddial43_0_vtk,
           motorBike_rrwhchainhub61_0_vtk, motorBike_rrwhrim32_0_vtk,
           motorBike_riderboots67_0_vtk, motorBike_riderhelmet65_0_vtk,
           motorBike_windshield26_0_vtk, motorBike_ridergloves68_0_vtk,
           motorBike_rrwhchainhubshadow89_0_vtk, motorBike_rrwhrim01010_0_vtk,
           motorBike_windshield0022_0_vtk, motorBike_rearseat24_0_vtk,
           motorBike_rrwhchainhub02222_0_vtk, motorBike_rearsusp45_0_vtk,
           motorBike_ridervisor66_0_vtk, motorBike_rrwhrim0055_0_vtk,
           motorBike_rrwhtyre41_0_vtk, motorBike_waterpipe54_0_vtk,
           motorBike_waterpump55_0_vtk, motorBike_rrwhrim005shadow17_0_vtk])

DataRepresentation180 = Show()
DataRepresentation180.EdgeColor = [0.0, 0.0, 0.5000076295109483]
DataRepresentation180.ScaleFactor = 0.20424311161041261
DataRepresentation180.SelectionPointFieldDataArrayName = 'k'
DataRepresentation180.SelectionCellFieldDataArrayName = 'k'

a1_p_PVLookupTable.RGBPoints = [-2.2786299436354954e+38, 0.23, 0.299, 0.754,
                                2.6372040827871255e+37, 0.706, 0.016, 0.15]

DataRepresentation180.ColorArrayName = 'p'
DataRepresentation180.LookupTable = a1_p_PVLookupTable

RenderView1 = GetRenderView()
AnimationScene1 = GetAnimationScene()
CameraAnimationCue6 = GetCameraTrack()
CameraAnimationCue6.Mode = 'Path-based'

TimeAnimationCue1 = GetTimeTrack()

KeyFrame20648 = CameraKeyFrame(FocalPathPoints=[0, 0, 0.6756975054740906],
                               FocalPoint=[0.0, 0.0, 0.75],
                               PositionPathPoints=[-15, 0, 0, 15, -1, 0, 5],
                               ClosedPositionPath=0, Position=[-1.0, 0.0, 0.0],
                               ViewUp=[0.0, 0.0, 1.0])

KeyFrame20649 = CameraKeyFrame(Position=[-1.0, 0.0, 0.0],
                               ViewUp=[0.0, 0.0, 1.0], KeyTime=1.0,
                               FocalPoint=[0.0, 0.0, 0.75])

CameraAnimationCue6.KeyFrames = [KeyFrame20648, KeyFrame20649]

RenderView1 = GetRenderView()

AnimationScene1 = GetAnimationScene()
AnimationScene1.EndTime = 1.0
AnimationScene1.NumberOfFrames = 50

WriteAnimation('vid.avi', Magnification=1, Quality=10, FrameRate=10.000000)

Render()

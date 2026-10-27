// Khách theo dõi đơn theo thời gian thực bằng SignalR (có dự phòng polling).
(() => {
  'use strict';
  const goc = document.getElementById('track');
  const maTheoDoi = goc.dataset.ma;
  const danhSachBuoc = [
    { ma: 1, bieuTuong: '📨', nhan: 'Đã gửi đơn' },
    { ma: 2, bieuTuong: '🧋', nhan: 'Đang pha chế' },
    { ma: 3, bieuTuong: '🔔', nhan: 'Sẵn sàng' },
    { ma: 4, bieuTuong: '✔', nhan: 'Hoàn thành' }
  ];
  const noiDung = {
    1: ['Đơn đã gửi tới quầy', 'Thu ngân sẽ nhận đơn trong giây lát. Bạn không cần tải lại trang.'],
    2: ['Quán đang pha chế cho bạn', 'Chờ một chút nhé, trang sẽ tự cập nhật khi đồ sẵn sàng.'],
    3: ['Đồ của bạn đã sẵn sàng!', 'Nhân viên sẽ mang đồ đến hoặc bạn ra quầy nhận nhé.'],
    4: ['Đơn đã hoàn thành', 'Cảm ơn bạn đã ghé quán. Hẹn gặp lại!'],
    5: ['Đơn đã bị hủy', 'Nếu có nhầm lẫn, vui lòng báo nhân viên tại quầy.']
  };
  const mangDi = goc.dataset.mangDi === '1';   // khách không ngồi bàn: phải thanh toán tại quầy trước khi quán làm
  let trangThaiTruoc = 0;

  function veLai(maTrangThai, daThanhToan) {
    const biHuy = maTrangThai === 5;
    const canThanhToanTruoc = mangDi && !daThanhToan && maTrangThai === 1;
    document.getElementById('headline').textContent = canThanhToanTruoc ? 'Vui lòng thanh toán tại quầy' : noiDung[maTrangThai][0];
    const the = document.getElementById('statusCard');
    the.textContent = canThanhToanTruoc
      ? 'Đơn mang đi cần thanh toán trước. Bạn hãy đến quầy, đọc mã đơn ' + maTheoDoi + ' cho thu ngân. Quán sẽ pha chế ngay sau khi nhận tiền.'
      : noiDung[maTrangThai][1];
    the.className = 'status-card' + (maTrangThai === 3 ? ' ready' : '') + (biHuy ? ' cancel' : '');
    document.getElementById('stepper').innerHTML = biHuy ? '' : danhSachBuoc.map((buoc) => {
      const lop = buoc.ma < maTrangThai || maTrangThai === 4 ? 'done' : buoc.ma === maTrangThai ? 'now' : '';
      return `<div class="st ${lop}"><div class="dot">${buoc.bieuTuong}</div>${buoc.nhan}</div>`;
    }).join('');
    const thanhToan = document.getElementById('payState');
    thanhToan.textContent = daThanhToan ? 'Đã thanh toán' : mangDi ? 'Cần thanh toán trước' : 'Thanh toán tại quầy';
    thanhToan.className = 'badge' + (daThanhToan ? ' ok' : mangDi ? ' warn' : '');
    if (maTrangThai === 3 && trangThaiTruoc !== 3) {
      document.title = 'Đồ của bạn đã sẵn sàng! - ' + maTheoDoi;
      if (navigator.vibrate) navigator.vibrate([200, 100, 200]);
    }
    trangThaiTruoc = maTrangThai;
    // Đã thanh toán hoặc đơn bị hủy thì không cần hiện mã QR nữa
    const khuQr = document.getElementById('khuQr');
    if (khuQr) khuQr.hidden = daThanhToan || biHuy;
    // Chỉ cho đánh giá khi đồ đã Sẵn sàng hoặc Hoàn thành
    const khuDanhGia = document.getElementById('khuDanhGia');
    if (khuDanhGia) khuDanhGia.hidden = !(maTrangThai === 3 || maTrangThai === 4);
  }

  async function lamMoi() {
    try {
      const phanHoi = await fetch('/DonHang/LayTrangThai?ma=' + encodeURIComponent(maTheoDoi), { cache: 'no-store' });
      if (phanHoi.ok) { const duLieu = await phanHoi.json(); veLai(duLieu.maTrangThai, duLieu.daThanhToan); }
    } catch { /* bỏ qua, lần sau thử lại */ }
  }

  // ---------- Gửi đánh giá ----------
  const nutGuiDanhGia = document.getElementById('nutGuiDanhGia');
  if (nutGuiDanhGia) nutGuiDanhGia.addEventListener('click', async () => {
    const thongBao = document.getElementById('thongBaoDanhGia');
    const danhSachMon = [];
    document.querySelectorAll('.rv-mon.chua[data-ma-chi-tiet]').forEach((khung) => {
      const chon = khung.querySelector('input[type=radio]:checked');
      if (!chon) return;   // món không chấm sao thì bỏ qua
      danhSachMon.push({ maChiTiet: +khung.dataset.maChiTiet, soSao: +chon.value, nhanXet: khung.querySelector('textarea').value });
    });
    const chonDichVu = document.querySelector('#rvDichVu input[type=radio]:checked');
    const oGopY = document.getElementById('gopY');
    if (!danhSachMon.length && !chonDichVu) { thongBao.textContent = 'Bạn hãy chấm ít nhất một mục bằng cách bấm vào các ngôi sao.'; return; }

    nutGuiDanhGia.disabled = true;
    thongBao.textContent = 'Đang gửi...';
    try {
      const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
      const phanHoi = await fetch('/DonHang/GuiDanhGia', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', RequestVerificationToken: token },
        body: JSON.stringify({ maTheoDoi, soSaoDichVu: chonDichVu ? +chonDichVu.value : null, gopY: oGopY ? oGopY.value : null, danhSachMon })
      });
      const ketQua = await phanHoi.json().catch(() => ({}));
      if (!phanHoi.ok || !ketQua.thanhCong) { thongBao.textContent = ketQua.thongBao || 'Chưa gửi được đánh giá, vui lòng thử lại.'; nutGuiDanhGia.disabled = false; return; }
      thongBao.textContent = ketQua.thongBao;
      setTimeout(() => location.reload(), 1200);   // tải lại để hiện dạng "đã đánh giá"
    } catch {
      thongBao.textContent = 'Mất kết nối mạng, vui lòng thử lại.';
      nutGuiDanhGia.disabled = false;
    }
  });

  // Nút sao chép số tài khoản / nội dung chuyển khoản
  document.addEventListener('click', async (e) => {
    const nut = e.target.closest('[data-chep]');
    if (!nut) return;
    try {
      await navigator.clipboard.writeText(document.getElementById(nut.dataset.chep).textContent.trim());
      nut.textContent = 'Đã chép';
    } catch { nut.textContent = 'Hãy chép tay'; }
    setTimeout(() => { nut.textContent = 'Sao chép'; }, 1500);
  });

  veLai(+goc.dataset.trangThai, goc.dataset.daThanhToan === '1');

  const ketNoi = new signalR.HubConnectionBuilder().withUrl('/hubs/donhang').withAutomaticReconnect().build();
  ketNoi.on('TrangThaiThayDoi', (duLieu) => veLai(duLieu.maTrangThai, duLieu.daThanhToan));
  ketNoi.onreconnected(async () => { await ketNoi.invoke('ThamGiaDon', maTheoDoi); lamMoi(); });
  async function batDau() {
    try { await ketNoi.start(); await ketNoi.invoke('ThamGiaDon', maTheoDoi); lamMoi(); }
    catch { setTimeout(batDau, 3000); }
  }
  batDau();
  setInterval(lamMoi, 30000);   // dự phòng nếu mạng chập chờn
})();

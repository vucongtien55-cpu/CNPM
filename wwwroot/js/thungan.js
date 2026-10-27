// Bảng đơn + ma trận bàn của thu ngân: cập nhật thời gian thực bằng SignalR.
(() => {
  'use strict';
  const $ = (chon) => document.querySelector(chon);
  const thoatHtml = (chuoi) => String(chuoi ?? '').replace(/[&<>"']/g, (k) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[k]));
  const dinhDangTien = (so) => Math.round(so).toString().replace(/\B(?=(\d{3})+(?!\d))/g, '.') + 'đ';
  const SO_DON_VUA_XONG_TOI_DA = 6;      // cột "Vừa xong" chỉ hiện tối đa từng này đơn mới nhất

  const donDaBiet = new Set();
  let daTaiLanDau = false;
  let amThanh = null, batAmThanh = false;
  let danhSachDon = [];
  let banDangLoc = null;                  // mã bàn đang được chọn để xem riêng

  function hienThongBao(noiDung) {
    const the = document.createElement('div');
    the.className = 'toast';
    the.textContent = noiDung;
    $('#toasts').appendChild(the);
    setTimeout(() => the.remove(), 4500);
  }

  function phatTiengBip() {
    if (!batAmThanh || !amThanh) return;
    [0, 0.18].forEach((tre) => {
      const daoDong = amThanh.createOscillator(), doLon = amThanh.createGain();
      daoDong.connect(doLon); doLon.connect(amThanh.destination);
      daoDong.frequency.value = 1046.5; doLon.gain.value = 0.2;
      daoDong.start(amThanh.currentTime + tre); daoDong.stop(amThanh.currentTime + tre + 0.15);
    });
  }

  /// Đọc to thông báo bằng giọng nói tiếng Việt của trình duyệt (Web Speech API). Trình duyệt không hỗ trợ thì bỏ qua.
  function docThongBao(cauNoi) {
    if (!('speechSynthesis' in window)) return;
    try {
      const cau = new SpeechSynthesisUtterance(cauNoi);
      cau.lang = 'vi-VN'; cau.rate = 1; cau.volume = 1;
      window.speechSynthesis.speak(cau);   // trình duyệt tự xếp hàng các câu speak() liên tiếp
    } catch { /* một số trình duyệt di động chặn phát giọng nói khi chưa có tương tác của người dùng */ }
  }

  /// Kêu "keng keng" rồi đọc câu thông báo. Chỉ phát khi thu ngân đã bật âm báo.
  function baoDonMoi(cauNoi) {
    if (!batAmThanh) return;
    phatTiengBip();
    setTimeout(() => docThongBao(cauNoi), 500);
  }

  const phutTu = (iso) => Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60000));
  const thoiGianTruoc = (iso) => { const phut = phutTu(iso); return phut < 1 ? 'Vừa xong' : phut + ' phút trước'; };

  // ---------- Ma trận bàn đang có khách ----------
  // Bàn "có khách" = có đơn đang xử lý (chờ xác nhận / đang làm / sẵn sàng) hoặc đã giao nhưng chưa thu tiền.
  function tinhBanCoKhach(ds) {
    const ban = new Map();
    ds.forEach((don) => {
      if (don.maBan == null) return;
      const dangCoKhach = don.maTrangThai <= 3 || (don.maTrangThai === 4 && !don.daThanhToan);
      if (!dangCoKhach) return;
      if (!ban.has(don.maBan)) ban.set(don.maBan, { maBan: don.maBan, ten: don.noiPhucVu, choXacNhan: 0, dangLam: 0, sanSang: 0, soDonChuaThu: 0, tienChuaThu: 0, somNhat: don.ngayTao });
      const b = ban.get(don.maBan);
      if (don.maTrangThai === 1) b.choXacNhan++;
      if (don.maTrangThai === 2) b.dangLam++;
      if (don.maTrangThai === 3) b.sanSang++;
      if (!don.daThanhToan) { b.soDonChuaThu++; b.tienChuaThu += don.tongTien; }
      if (new Date(don.ngayTao) < new Date(b.somNhat)) b.somNhat = don.ngayTao;
    });
    return [...ban.values()].sort((a, b) => a.ten.localeCompare(b.ten, 'vi', { numeric: true }));
  }

  function veTheBan(b) {
    const lop = b.choXacNhan ? 'b-do' : b.dangLam ? 'b-xanh' : b.sanSang ? 'b-luc' : 'b-vang';
    const nhan = [b.choXacNhan && `${b.choXacNhan} chờ xác nhận`, b.dangLam && `${b.dangLam} đang làm`, b.sanSang && `${b.sanSang} sẵn sàng`]
      .filter(Boolean).join(' - ') || 'Chờ thanh toán';
    const nutThu = b.tienChuaThu > 0
      ? `<div class="ban-nut">
           <button type="button" class="btn btn-sm btn-ink" data-hd="thu-ban" data-ma-ban="${b.maBan}" data-pt="1" data-ten="${thoatHtml(b.ten)}" data-tien="${b.tienChuaThu}">Thu cả bàn: tiền mặt</button>
           <button type="button" class="btn btn-sm btn-ink" data-hd="thu-ban" data-ma-ban="${b.maBan}" data-pt="2" data-ten="${thoatHtml(b.ten)}" data-tien="${b.tienChuaThu}">Chuyển khoản</button>
         </div>`
      : '';
    const nutChuyen = `<div class="ban-nut"><button type="button" class="btn btn-sm btn-ghost" data-hd="chuyen-ban" data-ma-ban="${b.maBan}" data-ten="${thoatHtml(b.ten)}">Chuyển / gộp bàn</button></div>`;
    return `<div class="ban-the ${lop}${banDangLoc === b.maBan ? ' dang-chon' : ''}" data-hd="loc-ban" data-ma-ban="${b.maBan}" tabindex="0" role="button" aria-pressed="${banDangLoc === b.maBan}" aria-label="${thoatHtml(b.ten)}: ${nhan}">
      <div class="ban-ten">${thoatHtml(b.ten)}</div>
      <div class="ban-thoi-gian">Có khách ${phutTu(b.somNhat)} phút</div>
      <div class="ban-nhan">${nhan}</div>
      <div class="ban-tien ${b.tienChuaThu > 0 ? 'no' : 'du'}">${b.tienChuaThu > 0 ? 'Chưa thu: ' + dinhDangTien(b.tienChuaThu) : 'Đã thu đủ'}</div>
      ${nutThu}${nutChuyen}
    </div>`;
  }

  function veKhuBan() {
    const danhSachBan = tinhBanCoKhach(danhSachDon);
    if (banDangLoc != null && !danhSachBan.some((b) => b.maBan === banDangLoc)) banDangLoc = null;   // bàn đã trống thì bỏ lọc
    $('#demBan').textContent = danhSachBan.length;
    $('#luoiBan').innerHTML = danhSachBan.length
      ? danhSachBan.map(veTheBan).join('')
      : '<p class="muted" style="margin:.4rem 0">Hiện chưa có bàn nào có khách.</p>';
    const banLoc = danhSachBan.find((b) => b.maBan === banDangLoc);
    $('#thanhLoc').hidden = !banLoc;
    if (banLoc) $('#chuLoc').textContent = `Đang chỉ xem đơn của ${banLoc.ten}`;
  }

  // ---------- Thẻ đơn ----------
  function veThe(don, laMoi) {
    const danhSachMon = don.danhSachMon.map((mon) => {
      const tuyChon = [mon.mucDa, mon.mucDuong, ...mon.danhSachTopping.map((t) => '+ ' + t)].filter(Boolean).join(', ');
      return `<li><span class="q">${mon.soLuong}x</span>${thoatHtml(mon.tenMon)}` +
        (tuyChon ? `<span class="sub">${thoatHtml(tuyChon)}</span>` : '') +
        (mon.ghiChu ? `<span class="sub">Ghi chú: ${thoatHtml(mon.ghiChu)}</span>` : '') + '</li>';
    }).join('');

    const taoNut = (nhan, kieu, thuocTinh) => `<button type="button" class="btn btn-sm ${kieu}" ${thuocTinh}>${nhan}</button>`;
    const doi = (tt) => `data-hd="doi" data-ma="${don.maDonHang}" data-tt="${tt}"`;
    const thu = (pt) => `data-hd="thu" data-ma="${don.maDonHang}" data-pt="${pt}"`;
    const chuaTraMangDi = don.mangDi && !don.daThanhToan && don.maTrangThai === 1;      // mang đi phải trả trước khi làm
    const thuocTinhHuy = `data-huy-don="${don.maDonHang}" data-huy-mo-ta="${thoatHtml(don.noiPhucVu + ' - ' + don.maTheoDoi)}" data-huy-da-thanh-toan="${don.daThanhToan ? 1 : 0}" data-huy-so-tien="${don.tongTien}"`;
    const nutHuy = taoNut('Hủy đơn', 'btn-ghost', thuocTinhHuy);

    let nut = '';
    if (chuaTraMangDi) {
      nut += taoNut('Thu tiền mặt', '', thu(1)) + taoNut('Thu chuyển khoản', '', thu(2)) + nutHuy;
    } else {
      if (don.maTrangThai === 1) nut += taoNut('Nhận đơn và làm', '', doi(2)) + nutHuy;
      if (don.maTrangThai === 2) nut += taoNut('Xong, gọi khách', 'btn-ok', doi(3)) + nutHuy;
      if (don.maTrangThai === 3) nut += taoNut('Đã giao khách', 'btn-ok', doi(4));
      if (!don.daThanhToan && don.maTrangThai !== 5) nut += taoNut('Thu tiền mặt', 'btn-ink', thu(1)) + taoNut('Thu chuyển khoản', 'btn-ink', thu(2));
      if (don.maBan) nut += taoNut('Tách bill', 'btn-ghost', `data-hd="tach" data-ma="${don.maDonHang}"`);
      nut += `<a class="btn btn-sm btn-ghost" href="/ThuNgan/InPhieu?ma=${don.maDonHang}" target="_blank" rel="noopener">In phiếu</a>`;
    }

    const nhan = don.maTrangThai === 4 ? '<span class="badge ok">Hoàn thành</span>' : don.maTrangThai === 5 ? '<span class="badge red">Đã hủy</span>' : '';
    return `<article class="o-card s${don.maTrangThai}${laMoi ? ' new' : ''}">
      <div class="o-top"><span class="o-place">${thoatHtml(don.noiPhucVu)}</span><span class="o-time">${thoiGianTruoc(don.ngayTao)}</span></div>
      <div class="o-code">${thoatHtml(don.maTheoDoi)}${don.tenKhach ? ' - ' + thoatHtml(don.tenKhach) : ''}</div>
      ${chuaTraMangDi ? '<div class="o-canh-bao">Khách mang đi: thu tiền xong mới được làm</div>' : ''}
      <ul class="o-items">${danhSachMon}</ul>
      ${don.ghiChu ? `<div class="o-note">Ghi chú: ${thoatHtml(don.ghiChu)}</div>` : ''}
      <div class="o-foot"><span>${dinhDangTien(don.tongTien)}</span>${nhan}<span class="badge ${don.daThanhToan ? 'ok' : 'warn'}">${don.daThanhToan ? 'Đã thu tiền' : 'Chưa thu tiền'}</span></div>
      <div class="o-actions">${nut}</div>
    </article>`;
  }

  function veBang(donMoi) {
    const hienThi = banDangLoc == null ? danhSachDon : danhSachDon.filter((don) => don.maBan === banDangLoc);
    const cot = { 1: [], 2: [], 3: [], 4: [] };
    hienThi.forEach((don) => {
      if (don.maTrangThai <= 3) cot[don.maTrangThai].push(don);
      else if (don.vuaXong) cot[4].push(don);           // chỉ đơn vừa xong / vừa hủy, do máy chủ quyết định
    });
    cot[4].sort((a, b) => new Date(b.ngayCapNhat || b.ngayTao) - new Date(a.ngayCapNhat || a.ngayTao));
    const conLai = cot[4].length - SO_DON_VUA_XONG_TOI_DA;
    cot[4] = cot[4].slice(0, SO_DON_VUA_XONG_TOI_DA);

    [1, 2, 3, 4].forEach((so) => {
      $('#dem' + so).textContent = cot[so].length;
      let noiDung = cot[so].length
        ? cot[so].map((don) => veThe(don, donMoi.has(don.maDonHang))).join('')
        : '<p class="muted" style="text-align:center;padding:1rem 0">Chưa có đơn</p>';
      if (so === 4 && conLai > 0) noiDung += `<p class="muted" style="text-align:center;font-size:.85rem">Và ${conLai} đơn khác, xem trong Lịch sử đơn.</p>`;
      $('#cot' + so).innerHTML = noiDung;
    });
  }

  const veTatCa = (donMoi = new Set()) => { veKhuBan(); veBang(donMoi); };

  async function taiLai() {
    try {
      const phanHoi = await fetch('/ThuNgan/DuLieuBang', { cache: 'no-store' });
      if (!phanHoi.ok) return;
      danhSachDon = await phanHoi.json();
      const donMoi = new Set();
      if (daTaiLanDau) danhSachDon.forEach((don) => { if (!donDaBiet.has(don.maDonHang)) donMoi.add(don.maDonHang); });
      danhSachDon.forEach((don) => donDaBiet.add(don.maDonHang));
      daTaiLanDau = true;
      veTatCa(donMoi);
    } catch { /* phiên đăng nhập hết hạn hoặc mất mạng: lần sau thử lại */ }
  }

  // ---------- Bấm nút / bấm bàn ----------
  const vungLamViec = document.querySelector('.main');
  vungLamViec.addEventListener('click', async (e) => {
    // 1) Thu tiền cả bàn
    const nutThuBan = e.target.closest('button[data-hd="thu-ban"]');
    if (nutThuBan) {
      const tenBan = nutThuBan.dataset.ten, soTien = +nutThuBan.dataset.tien, pt = +nutThuBan.dataset.pt;
      if (pt === 2) { if (!(await hienQrChuyenKhoan({ maBan: +nutThuBan.dataset.maBan }))) return; }   // khách quét VietQR, thu ngân kiểm tra tiền đã vào tài khoản
      else if (!confirm(`Thu ${dinhDangTien(soTien)} bằng tiền mặt cho ${tenBan}?`)) return;
      nutThuBan.disabled = true;
      const ketQua = await guiJson('/ThuNgan/ThuTienCaBan', { maBan: +nutThuBan.dataset.maBan, maPhuongThuc: pt });
      hienThongBao(ketQua.thanhCong ? `Đã thu ${dinhDangTien(ketQua.tongTien)} (${ketQua.soDon} đơn) của ${tenBan}` : (ketQua.thongBao || 'Chưa thu được tiền.'));
      taiLai();
      return;
    }
    // 1b) Chuyển / gộp bàn
    const nutChuyenBan = e.target.closest('button[data-hd="chuyen-ban"]');
    if (nutChuyenBan) {
      const chon = await hienChuyenBan(+nutChuyenBan.dataset.maBan, nutChuyenBan.dataset.ten);
      if (chon == null) return;
      const ketQua = await guiJson('/ThuNgan/ChuyenBan', { tuMaBan: +nutChuyenBan.dataset.maBan, denMaBan: chon });
      hienThongBao(ketQua.thanhCong ? `Đã chuyển ${ketQua.soDon} đơn sang bàn đã chọn` : (ketQua.thongBao || 'Chưa chuyển được bàn.'));
      taiLai();
      return;
    }

    // 2) Bấm vào thẻ bàn: lọc đơn theo bàn
    const theBan = e.target.closest('[data-hd="loc-ban"]');
    if (theBan) {
      const ma = +theBan.dataset.maBan;
      banDangLoc = banDangLoc === ma ? null : ma;
      veTatCa();
      return;
    }
    if (e.target.closest('#nutBoLoc')) { banDangLoc = null; veTatCa(); return; }

    // 3) Nút trên thẻ đơn
    const nut = e.target.closest('#bangDon button[data-hd]');
    if (!nut) return;
    const maDonHang = +nut.dataset.ma;
    let ketQua;
    if (nut.dataset.hd === 'doi') {
      nut.disabled = true;
      ketQua = await guiJson('/ThuNgan/CapNhatTrangThai', { maDonHang, maTrangThai: +nut.dataset.tt });
    } else if (nut.dataset.hd === 'tach') {
      const donDangTach = danhSachDon.find((d) => d.maDonHang === maDonHang);
      const chon = donDangTach ? await hienTachBill(donDangTach) : null;
      if (!chon || chon.length === 0) { taiLai(); return; }
      nut.disabled = true;
      ketQua = await guiJson('/ThuNgan/TachDon', { maDonHang, danhSachMaChiTiet: chon });
      if (ketQua.thanhCong) hienThongBao(`Đã tách thành đơn mới ${ketQua.maTheoDoiMoi}`);
    } else {
      if (+nut.dataset.pt === 2 && !(await hienQrChuyenKhoan({ maDonHang }))) return;   // chuyển khoản: hiện mã QR rồi mới xác nhận
      nut.disabled = true;
      ketQua = await guiJson('/ThuNgan/DanhDauDaThanhToan', { maDonHang, maPhuongThuc: +nut.dataset.pt });
    }
    if (!ketQua.thanhCong) hienThongBao(ketQua.thongBao || 'Chưa cập nhật được đơn.');
    taiLai();
  });

  // Cho hộp hủy đơn dùng chung (nhanvien.js) biết cách tải lại sau khi hủy thành công
  window.taiLaiSauKhiHuy = taiLai;

  // ---------- Hộp chọn món để tách bill: tick các món cần chuyển sang đơn mới cùng bàn ----------
  let hopTachBill = document.getElementById('hopTachBill');
  if (!hopTachBill) {
    hopTachBill = document.createElement('dialog');
    hopTachBill.id = 'hopTachBill';
    hopTachBill.className = 'opt';
    hopTachBill.innerHTML = `<form id="formTachBill" style="display:grid;gap:1rem;padding:1.4rem;max-height:90vh;overflow:auto">
        <h3 style="margin:0">Tách bill</h3>
        <p class="muted" style="margin:0">Tick những món cần chuyển sang bill mới (bill mới vẫn ở cùng bàn, chưa thanh toán).</p>
        <div id="dsTachBill" style="display:grid;gap:.5rem"></div>
        <p class="cart-msg" id="tachBillLoi" role="alert"></p>
        <div class="actions" style="justify-content:flex-end">
          <button type="button" class="btn btn-ghost" id="tachBillDong">Hủy</button>
          <button type="submit" class="btn btn-ink">Tách bill</button>
        </div>
      </form>`;
    document.body.appendChild(hopTachBill);
  }

  function hienTachBill(don) {
    return new Promise((xong) => {
      const dinhDangTien = (so) => Math.round(so).toString().replace(/\B(?=(\d{3})+(?!\d))/g, '.') + 'đ';
      document.getElementById('dsTachBill').innerHTML = don.danhSachMon.map((m) => `
        <label class="check" style="align-items:flex-start">
          <input type="checkbox" value="${m.maChiTiet}" />
          <span>${m.soLuong}x ${thoatHtml(m.tenMon)} - ${dinhDangTien(m.donGia)}
            ${[m.mucDa, m.mucDuong, ...m.danhSachTopping].filter(Boolean).map(thoatHtml).join(', ') ? `<br /><span class="muted" style="font-size:.82rem">${[m.mucDa, m.mucDuong, ...m.danhSachTopping].filter(Boolean).map(thoatHtml).join(', ')}</span>` : ''}
          </span>
        </label>`).join('');
      document.getElementById('tachBillLoi').textContent = '';
      let daXong = false;
      const ketThuc = (chon) => { if (daXong) return; daXong = true; hopTachBill.close(); xong(chon); };
      document.getElementById('tachBillDong').onclick = () => ketThuc(null);
      hopTachBill.oncancel = (e) => { e.preventDefault(); ketThuc(null); };
      hopTachBill.onclick = (e) => { if (e.target === hopTachBill) ketThuc(null); };
      document.getElementById('formTachBill').onsubmit = (e) => {
        e.preventDefault();
        const chon = [...document.querySelectorAll('#dsTachBill input:checked')].map((o) => +o.value);
        if (chon.length === 0) { document.getElementById('tachBillLoi').textContent = 'Hãy tick ít nhất một món.'; return; }
        if (chon.length === don.danhSachMon.length) { document.getElementById('tachBillLoi').textContent = 'Không thể tách hết toàn bộ món trong đơn.'; return; }
        ketThuc(chon);
      };
      hopTachBill.showModal();
    });
  }

  // ---------- Chuyển / gộp bàn: chọn bàn đích ----------
  async function hienChuyenBan(tuMaBan, tenBanDi) {
    let danhSachBan = [];
    try { danhSachBan = await (await fetch('/ThuNgan/DanhSachBan', { cache: 'no-store' })).json(); } catch { /* bỏ qua */ }
    const lua = danhSachBan.filter((b) => b.maBan !== tuMaBan);
    if (lua.length === 0) { hienThongBao('Không có bàn nào khác để chuyển đến.'); return null; }
    const danhSachChu = lua.map((b, i) => `${i + 1}. ${b.tenBan}`).join('\n');
    const nhap = prompt(`Chuyển toàn bộ đơn chưa thanh toán của ${tenBanDi} sang bàn nào?\nNhập số thứ tự:\n${danhSachChu}`);
    const i = parseInt(nhap, 10) - 1;
    if (!nhap || Number.isNaN(i) || i < 0 || i >= lua.length) return null;
    return lua[i].maBan;
  }

  // ---------- Thanh yêu cầu từ bàn (khách bấm "Gọi nhân viên" / "Xin thanh toán") ----------
  const thanhYeuCau = document.createElement('div');
  thanhYeuCau.id = 'thanhYeuCau';
  thanhYeuCau.className = 'thanh-yeu-cau no-print';
  thanhYeuCau.hidden = true;
  document.querySelector('.actions.no-print')?.after(thanhYeuCau);

  const yeuCauDaBiet = new Set();
  let daTaiYeuCauLanDau = false;
  const thongDiepYeuCau = (loai) => loai === 'GOI_NHAN_VIEN' ? 'Có khách gọi nhân viên' : 'Có khách xin thanh toán';

  async function taiYeuCau() {
    try {
      const phanHoi = await fetch('/ThuNgan/DuLieuYeuCau', { cache: 'no-store' });
      if (!phanHoi.ok) return;
      const danhSach = await phanHoi.json();
      if (daTaiYeuCauLanDau)
        danhSach.filter((y) => !yeuCauDaBiet.has(y.maYeuCau)).forEach((y) => baoDonMoi(`${thongDiepYeuCau(y.loaiYeuCau)}, ${y.tenBan}`));
      danhSach.forEach((y) => yeuCauDaBiet.add(y.maYeuCau));
      daTaiYeuCauLanDau = true;

      thanhYeuCau.hidden = danhSach.length === 0;
      thanhYeuCau.innerHTML = danhSach.map((y) => `<span class="yc-the">
          <b>${thoatHtml(y.tenBan)}</b>: ${y.loaiYeuCau === 'GOI_NHAN_VIEN' ? '🙋 Gọi nhân viên' : '💳 Xin thanh toán'}
          <button type="button" class="btn btn-sm btn-ok" data-xu-ly-yc="${y.maYeuCau}">Đã xử lý</button>
        </span>`).join('');
    } catch { /* bỏ qua, lần sau thử lại */ }
  }
  thanhYeuCau.addEventListener('click', async (e) => {
    const nut = e.target.closest('[data-xu-ly-yc]');
    if (!nut) return;
    nut.disabled = true;
    await guiJson('/ThuNgan/XuLyYeuCau', { ma: +nut.dataset.xuLyYc });
    taiYeuCau();
  });
  taiYeuCau();
  setInterval(taiYeuCau, 20000);
  window.__taiYeuCauTaiBan = taiYeuCau;   // SignalR phía dưới sẽ gọi lại khi có sự kiện YeuCauThayDoi

  vungLamViec.addEventListener('keydown', (e) => {
    if ((e.key === 'Enter' || e.key === ' ') && e.target.matches('[data-hd="loc-ban"]')) { e.preventDefault(); e.target.click(); }
  });

  $('#nutTaiLai').addEventListener('click', taiLai);
  $('#nutAmThanh').addEventListener('click', (e) => {
    batAmThanh = !batAmThanh;
    if (batAmThanh && !amThanh) amThanh = new (window.AudioContext || window.webkitAudioContext)();
    e.target.textContent = batAmThanh ? '🔕 Tắt âm báo đơn mới' : '🔔 Bật âm báo đơn mới';
    if (batAmThanh) baoDonMoi('Đã bật âm báo đơn mới');
  });

  // ---------- SignalR ----------
  const nhanKetNoi = $('#conn');
  const datTrangThaiKetNoi = (dangNoi) => {
    nhanKetNoi.textContent = dangNoi ? 'Đang nhận đơn trực tiếp' : 'Mất kết nối, đang thử lại...';
    nhanKetNoi.className = 'conn ' + (dangNoi ? 'on' : 'off');
  };
  const ketNoi = new signalR.HubConnectionBuilder().withUrl('/hubs/donhang').withAutomaticReconnect().build();
  ketNoi.on('CoDonMoi', (thongTin) => {
    hienThongBao((thongTin.mangDi ? 'Đơn mang đi mới, cần thu tiền: ' : 'Đơn mới từ ') + thongTin.noiPhucVu);
    baoDonMoi(thongTin.mangDi ? `Có đơn mới, mang đi, cần thu tiền` : `Có đơn mới, ${thongTin.noiPhucVu}`);
    taiLai();
  });
  ketNoi.on('DonThayDoi', taiLai);
  ketNoi.on('YeuCauThayDoi', () => window.__taiYeuCauTaiBan?.());
  ketNoi.onreconnecting(() => datTrangThaiKetNoi(false));
  ketNoi.onreconnected(async () => { await ketNoi.invoke('ThamGiaNhanVien'); datTrangThaiKetNoi(true); taiLai(); });
  ketNoi.onclose(() => datTrangThaiKetNoi(false));
  async function batDau() {
    try { await ketNoi.start(); await ketNoi.invoke('ThamGiaNhanVien'); datTrangThaiKetNoi(true); taiLai(); }
    catch { datTrangThaiKetNoi(false); setTimeout(batDau, 3000); }
  }

  taiLai();
  batDau();
  setInterval(taiLai, 30000);   // dự phòng + cập nhật "x phút trước"
})();

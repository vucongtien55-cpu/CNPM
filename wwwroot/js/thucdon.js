// Giỏ hàng phía khách: lưu trong localStorage, gửi lên server bằng fetch.
(() => {
  'use strict';
  const KHOA_LUU = 'mx_gio_hang_v1';
  const cauHinh = JSON.parse(document.getElementById('menu-data').textContent);
  const $ = (chon) => document.querySelector(chon);
  const dinhDangTien = (so) => Math.round(so).toString().replace(/\B(?=(\d{3})+(?!\d))/g, '.') + 'đ';
  const thoatHtml = (chuoi) => String(chuoi ?? '').replace(/[&<>"']/g, (k) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[k]));
  const layToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

  let gioHang = [];
  try { gioHang = JSON.parse(localStorage.getItem(KHOA_LUU)) || []; } catch { gioHang = []; }
  const luuGioHang = () => { try { localStorage.setItem(KHOA_LUU, JSON.stringify(gioHang)); } catch { /* trình duyệt chặn storage */ } };
  const tienMotDong = (dong) => (dong.gia + dong.danhSachTopping.reduce((tong, t) => tong + t.gia, 0)) * dong.soLuong;

  // ---------- Giỏ hàng ----------
  const phanTu = {
    thanh: $('#cartBar'), soLuong: $('#barCount'), thanhTien: $('#barTotal'), gio: $('#cart'), nen: $('#scrim'),
    danhSach: $('#cartItems'), tongTien: $('#cartTotal'), thongBao: $('#cartMsg'), nutGui: $('#sendOrder'),
    goiY: $('#khuHayMuaCung')
  };

  // ---------- Mã giảm giá ----------
  let maDaApDung = null;   // { maCode, soTienGiam }

  const layGiaTheoSize = (maMonAn) => cauHinh.giaTheoSize[String(maMonAn)] || [];

  function tienGoc() { return gioHang.reduce((tong, d) => tong + tienMotDong(d), 0); }

  function capNhatTongTien() {
    const tongGoc = tienGoc();
    const coGiam = maDaApDung && maDaApDung.soTienGiam > 0;
    const tongSauGiam = coGiam ? Math.max(0, tongGoc - maDaApDung.soTienGiam) : tongGoc;
    $('#hangTongGoc').style.display = coGiam ? '' : 'none';
    $('#hangTienGiam').style.display = coGiam ? '' : 'none';
    if (coGiam) {
      $('#cartTongGoc').textContent = dinhDangTien(tongGoc);
      $('#cartTienGiam').textContent = '-' + dinhDangTien(maDaApDung.soTienGiam);
    }
    phanTu.thanhTien.textContent = dinhDangTien(tongGoc);
    phanTu.tongTien.textContent = dinhDangTien(tongSauGiam);
  }

  function veLaiGioHang() {
    const tongSoLuong = gioHang.reduce((tong, d) => tong + d.soLuong, 0);
    phanTu.thanh.classList.toggle('show', tongSoLuong > 0);
    phanTu.soLuong.textContent = tongSoLuong;
    phanTu.nutGui.disabled = gioHang.length === 0;
    phanTu.danhSach.innerHTML = gioHang.length
      ? gioHang.map((dong, i) => {
          const tuyChon = [dong.tenKichCo ? 'Size ' + dong.tenKichCo : null, dong.tenMucDa, dong.tenMucDuong, ...dong.danhSachTopping.map((t) => '+ ' + t.ten)].filter(Boolean).join(', ');
          return `<div class="cart-line">
            <div class="n">${thoatHtml(dong.ten)}</div><div class="lt">${dinhDangTien(tienMotDong(dong))}</div>
            ${tuyChon ? `<div class="opts">${thoatHtml(tuyChon)}</div>` : ''}
            ${dong.ghiChu ? `<div class="opts">Ghi chú: ${thoatHtml(dong.ghiChu)}</div>` : ''}
            <div class="qty">
              <button type="button" data-hanh-dong="giam" data-vi-tri="${i}" aria-label="Giảm">−</button>
              <span>${dong.soLuong}</span>
              <button type="button" data-hanh-dong="tang" data-vi-tri="${i}" aria-label="Tăng">+</button>
              <button type="button" class="rm" data-hanh-dong="xoa" data-vi-tri="${i}">Xóa</button>
            </div></div>`;
        }).join('')
      : '<div class="cart-empty">Giỏ hàng đang trống.<br />Chọn món yêu thích để bắt đầu.</div>';
    capNhatTongTien();
    luuGioHang();
    if (gioHang.length) taiGoiYHayMuaCung(gioHang[gioHang.length - 1].maMonAn);
    else { phanTu.goiY.classList.add('hidden'); phanTu.goiY.innerHTML = ''; }
  }

  function themVaoGio(dongMoi) {
    const khoa = [dongMoi.maMonAn, dongMoi.maKichCo, dongMoi.maMucDa, dongMoi.maMucDuong, dongMoi.danhSachTopping.map((t) => t.ma).sort().join('-'), dongMoi.ghiChu].join('|');
    const daCo = gioHang.find((d) => d.khoa === khoa);
    if (daCo) daCo.soLuong = Math.min(20, daCo.soLuong + dongMoi.soLuong);
    else gioHang.push({ ...dongMoi, khoa });
    veLaiGioHang();
  }

  // ---------- Gợi ý "khách hay mua cùng" ----------
  let bomGoiY = null;
  function taiGoiYHayMuaCung(maMonAn) {
    clearTimeout(bomGoiY);
    bomGoiY = setTimeout(async () => {
      try {
        const phanHoi = await fetch('/ThucDon/MonHayMuaCung?maMon=' + maMonAn, { cache: 'no-store' });
        if (!phanHoi.ok) throw new Error();
        const danhSach = await phanHoi.json();
        if (!Array.isArray(danhSach) || danhSach.length === 0) { phanTu.goiY.classList.add('hidden'); phanTu.goiY.innerHTML = ''; return; }
        phanTu.goiY.classList.remove('hidden');
        phanTu.goiY.innerHTML = '<div class="hmc-tieu-de">Khách hay mua cùng</div><div class="hmc-cuon">' +
          danhSach.map((m) => `<button type="button" class="goi-y-the-nho" data-ma="${m.maMonAn}" data-ten="${thoatHtml(m.tenMon)}" data-gia="${Math.round(m.gia)}" data-tuy-chon="${m.coTuyChon ? '1' : '0'}">
            <div class="goi-y-the-anh">${m.duongDanAnh ? `<img src="${m.duongDanAnh}" alt="" loading="lazy" />` : '<span class="emoji">🧋</span>'}</div>
            <div class="goi-y-the-ten">${thoatHtml(m.tenMon)}</div>
            <div class="goi-y-the-gia">${dinhDangTien(m.gia)}</div>
          </button>`).join('') + '</div>';
      } catch { /* im lặng bỏ qua, gợi ý không bắt buộc */ }
    }, 400);
  }

  const moGio = () => { phanTu.gio.classList.add('show'); phanTu.nen.classList.add('show'); };
  const dongGio = () => { phanTu.gio.classList.remove('show'); phanTu.nen.classList.remove('show'); };
  phanTu.thanh.addEventListener('click', moGio);
  phanTu.thanh.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); moGio(); } });
  $('#cartClose').addEventListener('click', dongGio);
  phanTu.nen.addEventListener('click', dongGio);
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape') dongGio(); });

  phanTu.danhSach.addEventListener('click', (e) => {
    const nut = e.target.closest('button[data-hanh-dong]');
    if (!nut) return;
    const viTri = +nut.dataset.viTri;
    if (nut.dataset.hanhDong === 'tang') gioHang[viTri].soLuong = Math.min(20, gioHang[viTri].soLuong + 1);
    if (nut.dataset.hanhDong === 'giam') gioHang[viTri].soLuong = Math.max(1, gioHang[viTri].soLuong - 1);
    if (nut.dataset.hanhDong === 'xoa') gioHang.splice(viTri, 1);
    veLaiGioHang();
  });

  // ---------- Hộp chọn tùy chọn món ----------
  const hop = $('#optDialog');
  let monDangChon = null, soLuongDangChon = 1;

  function sizeDangChon() {
    if (!monDangChon.coSize) return null;
    const radio = document.querySelector('#sizeChoices input:checked');
    return radio ? { ma: +radio.value, ten: radio.dataset.ten, gia: +radio.dataset.gia } : null;
  }
  function giaMonDangChon() {
    const size = sizeDangChon();
    return size ? size.gia : monDangChon.gia;
  }
  function tinhTienDangChon() {
    const tienTopping = [...document.querySelectorAll('#topChoices input:checked')].reduce((tong, x) => tong + +x.dataset.gia, 0);
    return (giaMonDangChon() + tienTopping) * soLuongDangChon;
  }
  function capNhatNutThem() {
    $('#optQty').textContent = soLuongDangChon;
    $('#optPrice').textContent = dinhDangTien(giaMonDangChon());
    $('#optAdd').textContent = 'Thêm vào giỏ - ' + dinhDangTien(tinhTienDangChon());
  }

  function moHopChonMon(nut) {
    const ma = +nut.dataset.ma;
    const sizes = layGiaTheoSize(ma);
    const coTuyChonDoUong = nut.dataset.tuyChon === '1';
    monDangChon = { ma, ten: nut.dataset.ten, gia: +nut.dataset.gia, coSize: sizes.length > 0, sizes };

    if (!coTuyChonDoUong && !monDangChon.coSize) {   // món kem/sundae không size: không có đá/đường/topping/size
      themVaoGio({ maMonAn: monDangChon.ma, ten: monDangChon.ten, gia: monDangChon.gia, soLuong: 1, maKichCo: null, tenKichCo: '', maMucDa: null, tenMucDa: '', maMucDuong: null, tenMucDuong: '', danhSachTopping: [], ghiChu: '' });
      const chuCu = nut.textContent;
      nut.textContent = 'Đã thêm';
      setTimeout(() => (nut.textContent = chuCu), 900);
      return;
    }
    soLuongDangChon = 1;
    $('#optName').textContent = monDangChon.ten;
    $('#optNote').value = '';

    $('#sizeFieldset').hidden = !monDangChon.coSize;
    if (monDangChon.coSize) {
      $('#sizeChoices').innerHTML = sizes.map((x, i) => `<label class="choice"><input type="radio" name="kichCo" value="${x.ma}" data-ten="${thoatHtml(x.ten)}" data-gia="${x.gia}" ${i === 0 ? 'checked' : ''} /><span>${thoatHtml(x.ten)} - ${dinhDangTien(x.gia)}</span></label>`).join('');
    }

    $('#iceChoices').closest('fieldset').hidden = !coTuyChonDoUong;
    $('#sugarChoices').closest('fieldset').hidden = !coTuyChonDoUong;
    $('#topChoices').closest('fieldset').hidden = !coTuyChonDoUong;
    if (coTuyChonDoUong) {
      $('#iceChoices').innerHTML = cauHinh.danhSachMucDa.map((x, i) => `<label class="choice"><input type="radio" name="mucDa" value="${x.ma}" data-ten="${thoatHtml(x.ten)}" ${i === 0 ? 'checked' : ''} /><span>${thoatHtml(x.ten)}</span></label>`).join('');
      $('#sugarChoices').innerHTML = cauHinh.danhSachMucDuong.map((x, i) => `<label class="choice"><input type="radio" name="mucDuong" value="${x.ma}" data-ten="${thoatHtml(x.ten)}" ${i === 0 ? 'checked' : ''} /><span>${thoatHtml(x.ten)}</span></label>`).join('');
      const toppingConBan = cauHinh.danhSachTopping.filter((t) => !t.hetHang);
      $('#topChoices').innerHTML = toppingConBan.length
        ? toppingConBan.map((t) => `<label class="choice"><input type="checkbox" value="${t.ma}" data-ten="${thoatHtml(t.ten)}" data-gia="${t.gia}" /><span>${thoatHtml(t.ten)} +${dinhDangTien(t.gia)}</span></label>`).join('')
        : '<span class="muted">Hiện chưa có topping.</span>';
    }
    capNhatNutThem();
    hop.showModal();
  }

  // Ủy quyền ở mức document: bắt được cả nút trên lưới món (có sẵn từ đầu) lẫn nút trong khu gợi ý (chèn động bằng JS)
  document.addEventListener('click', (e) => {
    const nut = e.target.closest('.add-btn, .goi-y-the-nho');
    if (nut) moHopChonMon(nut);
    const theBanChay = e.target.closest('.goi-y-the[data-tim-toi]');
    if (theBanChay) {
      const dich = document.getElementById(theBanChay.dataset.timToi);
      if (dich) {
        dich.scrollIntoView({ behavior: 'smooth', block: 'center' });
        dich.classList.add('nhay-sang');
        setTimeout(() => dich.classList.remove('nhay-sang'), 1500);
      }
    }
  });

  hop.addEventListener('change', capNhatNutThem);
  $('#optMinus').addEventListener('click', () => { soLuongDangChon = Math.max(1, soLuongDangChon - 1); capNhatNutThem(); });
  $('#optPlus').addEventListener('click', () => { soLuongDangChon = Math.min(20, soLuongDangChon + 1); capNhatNutThem(); });
  $('#optCancel').addEventListener('click', () => hop.close());
  hop.addEventListener('click', (e) => { if (e.target === hop) hop.close(); });

  $('#optForm').addEventListener('submit', (e) => {
    e.preventDefault();
    if (monDangChon.coSize && !sizeDangChon()) { return; }   // bắt buộc chọn size nếu món có size
    const mucDa = document.querySelector('#iceChoices input:checked');
    const mucDuong = document.querySelector('#sugarChoices input:checked');
    const size = sizeDangChon();
    const danhSachTopping = [...document.querySelectorAll('#topChoices input:checked')].slice(0, 5)
      .map((x) => ({ ma: +x.value, ten: x.dataset.ten, gia: +x.dataset.gia }));
    themVaoGio({
      maMonAn: monDangChon.ma, ten: monDangChon.ten, gia: giaMonDangChon(), soLuong: soLuongDangChon,
      maKichCo: size ? size.ma : null, tenKichCo: size ? size.ten : '',
      maMucDa: mucDa ? +mucDa.value : null, tenMucDa: mucDa ? mucDa.dataset.ten : '',
      maMucDuong: mucDuong ? +mucDuong.value : null, tenMucDuong: mucDuong ? mucDuong.dataset.ten : '',
      danhSachTopping, ghiChu: $('#optNote').value.trim()
    });
    hop.close();
  });

  // ---------- Áp dụng mã giảm giá ----------
  $('#apDungMa').addEventListener('click', async () => {
    const oMa = $('#maGiamGia');
    const oKetQua = $('#maGiamGiaKetQua');
    const ma = oMa.value.trim();
    if (!ma) { oKetQua.textContent = 'Vui lòng nhập mã giảm giá.'; oKetQua.className = 'ma-giam-gia-ket-qua khong-hop-le'; return; }
    if (gioHang.length === 0) { oKetQua.textContent = 'Giỏ hàng đang trống.'; oKetQua.className = 'ma-giam-gia-ket-qua khong-hop-le'; return; }
    $('#apDungMa').disabled = true;
    try {
      const phanHoi = await fetch('/DonHang/KiemTraMaGiamGia', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', RequestVerificationToken: layToken() },
        body: JSON.stringify({
          maCode: ma,
          danhSachMon: gioHang.map((d) => ({ maMonAn: d.maMonAn, soLuong: d.soLuong, maKichCo: d.maKichCo, danhSachMaTopping: d.danhSachTopping.map((t) => t.ma) }))
        })
      });
      const ketQua = await phanHoi.json().catch(() => ({}));
      oKetQua.classList.remove('hidden');
      oKetQua.textContent = ketQua.thongBao || 'Có lỗi xảy ra, vui lòng thử lại.';
      if (ketQua.hopLe) {
        oKetQua.className = 'ma-giam-gia-ket-qua hop-le';
        maDaApDung = { maCode: ma, soTienGiam: ketQua.soTienGiam };
      } else {
        oKetQua.className = 'ma-giam-gia-ket-qua khong-hop-le';
        maDaApDung = null;
      }
      capNhatTongTien();
    } catch {
      oKetQua.classList.remove('hidden');
      oKetQua.className = 'ma-giam-gia-ket-qua khong-hop-le';
      oKetQua.textContent = 'Mất kết nối mạng, vui lòng thử lại.';
    } finally {
      $('#apDungMa').disabled = false;
    }
  });

  // ---------- Gửi đơn ----------
  phanTu.nutGui.addEventListener('click', async () => {
    phanTu.thongBao.textContent = '';
    if (!cauHinh.duocDatHang) { phanTu.thongBao.textContent = 'Quán hiện chưa nhận đơn, vui lòng tải lại trang để xem thông báo mới nhất.'; return; }
    phanTu.nutGui.disabled = true;
    phanTu.nutGui.textContent = 'Đang gửi đơn...';
    try {
      const phanHoi = await fetch('/DonHang/DatHang', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', RequestVerificationToken: layToken() },
        body: JSON.stringify({
          tenKhach: $('#custName').value, ghiChu: $('#custNote').value,
          soDienThoai: $('#custPhone').value.trim(), maGiamGia: maDaApDung ? maDaApDung.maCode : null,
          danhSachMon: gioHang.map((d) => ({ maMonAn: d.maMonAn, soLuong: d.soLuong, maKichCo: d.maKichCo, maMucDa: d.maMucDa, maMucDuong: d.maMucDuong, danhSachMaTopping: d.danhSachTopping.map((t) => t.ma), ghiChu: d.ghiChu }))
        })
      });
      const ketQua = await phanHoi.json().catch(() => ({}));
      if (!phanHoi.ok || !ketQua.thanhCong) { phanTu.thongBao.textContent = ketQua.thongBao || 'Chưa gửi được đơn. Vui lòng tải lại trang và thử lại.'; return; }
      gioHang = []; maDaApDung = null; luuGioHang();
      location.href = ketQua.duongDan;
    } catch {
      phanTu.thongBao.textContent = 'Mất kết nối mạng. Kiểm tra Wi-Fi rồi bấm gửi lại.';
    } finally {
      phanTu.nutGui.textContent = 'Gửi đơn cho quán';
      phanTu.nutGui.disabled = gioHang.length === 0;
    }
  });

  // ---------- Xem đánh giá của một món ----------
  const hopDanhGia = $('#dgDialog');
  const veSao = (diem) => `<span class="sao" style="--diem:${Number(diem).toFixed(2)}" role="img" aria-label="${Number(diem).toFixed(1)} trên 5 sao"></span>`;

  function veNoiDungDanhGia(duLieu) {
    if (!duLieu.soLuot) return '<p class="muted">Món này chưa có đánh giá.</p>';
    const theoSao = {};
    duLieu.phanBo.forEach((p) => { theoSao[p.soSao] = p.soLuot; });
    const phanBo = [5, 4, 3, 2, 1].map((sao) => {
      const so = theoSao[sao] || 0;
      const phanTram = Math.round((so / duLieu.soLuot) * 100);
      return `<div class="dg-hang"><span>${sao} sao</span><div class="dg-thanh"><i style="width:${phanTram}%"></i></div><span>${so}</span></div>`;
    }).join('');
    const nhanXet = duLieu.danhSach.length
      ? duLieu.danhSach.map((x) => `<div class="dg-muc">${veSao(x.soSao)} <b>${thoatHtml(x.tenKhach || 'Khách')}</b>
          <span class="muted">${new Date(x.ngayTao).toLocaleDateString('vi-VN')}</span><p>${thoatHtml(x.nhanXet)}</p></div>`).join('')
      : '<p class="muted">Chưa có nhận xét bằng chữ.</p>';
    return `<div class="dg-tom-tat"><div class="dg-lon">${Number(duLieu.diemTrungBinh).toFixed(1)}</div>
        <div>${veSao(duLieu.diemTrungBinh)}<div class="muted">${duLieu.soLuot} người đã đánh giá</div></div></div>
      <div class="dg-phan-bo">${phanBo}</div><h4 style="margin:0 0 .3rem">Nhận xét gần đây</h4>${nhanXet}`;
  }

  document.querySelectorAll('.danh-gia-nut').forEach((nut) => nut.addEventListener('click', async () => {
    $('#dgTen').textContent = nut.dataset.ten;
    $('#dgNoiDung').innerHTML = '<p class="muted">Đang tải đánh giá...</p>';
    hopDanhGia.showModal();
    try {
      const phanHoi = await fetch('/ThucDon/DanhGiaMon?ma=' + encodeURIComponent(nut.dataset.ma), { cache: 'no-store' });
      if (!phanHoi.ok) throw new Error();
      $('#dgNoiDung').innerHTML = veNoiDungDanhGia(await phanHoi.json());
    } catch {
      $('#dgNoiDung').innerHTML = '<p class="cart-msg">Không tải được đánh giá. Vui lòng thử lại.</p>';
    }
  }));
  $('#dgDong').addEventListener('click', () => hopDanhGia.close());
  hopDanhGia.addEventListener('click', (e) => { if (e.target === hopDanhGia) hopDanhGia.close(); });

  // ---------- Đánh dấu nhóm món đang xem ----------
  const danhSachThe = [...document.querySelectorAll('.cat-chip')];
  if ('IntersectionObserver' in window) {
    const quanSat = new IntersectionObserver((danhSachMuc) => {
      danhSachMuc.forEach((muc) => {
        if (muc.isIntersecting) danhSachThe.forEach((the) => the.classList.toggle('active', the.dataset.cat === muc.target.id));
      });
    }, { rootMargin: '-140px 0px -60% 0px' });
    document.querySelectorAll('.menu-cat').forEach((khu) => quanSat.observe(khu));
  }

  veLaiGioHang();
})();

// ---------- Tìm món (gõ có dấu hoặc không dấu đều tìm được) ----------
(() => {
  const boDau = (chuoi) => String(chuoi ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();
  const oTim = document.getElementById('timMon');
  const nutXoa = document.getElementById('timMonXoa');
  if (!oTim) return;

  const tatCaThe = [...document.querySelectorAll('.p-card[data-tim]')];
  const tatCaNhom = [...document.querySelectorAll('.menu-cat')];
  const oKhongThay = document.getElementById('timMonKhongThay');
  const thanhNhom = document.querySelector('.cat-nav');

  function locMon() {
    const tuKhoa = boDau(oTim.value.trim());
    nutXoa.classList.toggle('hidden', !tuKhoa);
    if (!tuKhoa) {
      tatCaThe.forEach((the) => { the.hidden = false; });
      tatCaNhom.forEach((nhom) => { nhom.hidden = false; });
      oKhongThay.classList.add('hidden');
      if (thanhNhom) thanhNhom.hidden = false;
      return;
    }
    if (thanhNhom) thanhNhom.hidden = true;   // đang tìm kiếm thì ẩn thanh nhảy nhóm cho gọn
    let coKetQua = false;
    tatCaNhom.forEach((nhom) => {
      let conMon = false;
      nhom.querySelectorAll('.p-card[data-tim]').forEach((the) => {
        const khop = boDau(the.dataset.tim).includes(tuKhoa);
        the.hidden = !khop;
        if (khop) conMon = true;
      });
      nhom.hidden = !conMon;
      if (conMon) coKetQua = true;
    });
    oKhongThay.classList.toggle('hidden', coKetQua);
  }

  oTim.addEventListener('input', locMon);
  nutXoa.addEventListener('click', () => { oTim.value = ''; locMon(); oTim.focus(); });
})();
